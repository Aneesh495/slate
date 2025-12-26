using System.Diagnostics;
using Slate.Core.Catalog;
using Slate.Core.Execution;
using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Slate.Core.Storage;
using Slate.Core.Transactions;
using Slate.Core.Udf;

namespace Slate.Engine.Kernel;

public sealed record QueryResult(
    TableSchema Schema,
    List<SlateValue[]> Rows,
    int RowsAffected,
    double ExecutionTimeMs,
    string? ExplainPlan = null);

/// <summary>
/// Top-level Slate HTAP database engine orchestrating transactional storage,
/// vector indexes, query planning, and vectorized execution.
/// </summary>
public sealed class SlateDatabase : IDisposable
{
    private readonly CatalogManager _catalog = new();
    private readonly MvccManager _mvcc = new();
    private readonly EpochGc _epochGc = new();
    private readonly UdfRegistry _udfs = new();
    private readonly Dictionary<string, MemTable> _memTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HnswIndex> _hnswIndexes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BPlusTree<SlateValue, SlateValue[]>> _bTrees = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private readonly Stream _walStream;
    private readonly WalWriter _walWriter;

    public CatalogManager Catalog => _catalog;
    public MvccManager Mvcc => _mvcc;
    public UdfRegistry Udfs => _udfs;

    public SlateDatabase(Stream? walStream = null)
    {
        _walStream = walStream ?? new MemoryStream();
        _walWriter = new WalWriter(_walStream);
    }

    public TransactionContext BeginTransaction(IsolationLevel isolation = IsolationLevel.SnapshotIsolation)
    {
        return _mvcc.BeginTransaction(isolation);
    }

    public void CommitTransaction(TransactionContext tx)
    {
        _mvcc.CommitTransaction(tx, _walWriter);
        lock (_lock)
        {
            foreach (var key in tx.WriteSet)
            {
                if (_memTables.TryGetValue(key.TableName, out var memTable))
                {
                    memTable.Commit(key.Key, tx.TxId, tx.CommitTimestamp);
                }
            }
        }
    }

    public void AbortTransaction(TransactionContext tx)
    {
        _mvcc.AbortTransaction(tx, _walWriter);
        lock (_lock)
        {
            foreach (var key in tx.WriteSet)
            {
                if (_memTables.TryGetValue(key.TableName, out var memTable))
                {
                    memTable.Rollback(key.Key, tx.TxId);
                }
            }
        }
    }

    public QueryResult Execute(string sql, TransactionContext? externalTx = null)
    {
        var sw = Stopwatch.StartNew();
        var ast = XqlParser.Parse(sql);

        // Check if transaction control statement
        if (ast is TransactionStatement txStmt)
        {
            sw.Stop();
            return new QueryResult(
                new TableSchema("tx", new[] { new ColumnDescriptor("status", DataType.String) }),
                new List<SlateValue[]> { new[] { SlateValue.FromString(txStmt.Action.ToString()) } },
                0,
                sw.Elapsed.TotalMilliseconds);
        }

        // Semantic analysis
        var analyzer = new XqlSemanticAnalyzer(_catalog);
        var analysis = analyzer.Analyze(ast);

        switch (ast)
        {
            case CreateTableStatement createTable:
                return ExecuteCreateTable(createTable, sw);

            case CreateIndexStatement createIndex:
                return ExecuteCreateIndex(createIndex, sw);

            case DropTableStatement dropTable:
                return ExecuteDropTable(dropTable, sw);

            case InsertStatement insert:
                return ExecuteInsert(insert, externalTx, sw);

            case SelectStatement select:
                return ExecuteSelect(select, externalTx, sw);

            case ExplainStatement explain:
                return ExecuteExplain(explain.TargetStatement, sw);

            default:
                throw new NotSupportedException($"Statement type '{ast.GetType().Name}' is not supported yet.");
        }
    }

    private QueryResult ExecuteCreateTable(CreateTableStatement createTable, Stopwatch sw)
    {
        lock (_lock)
        {
            var cols = createTable.Columns.Select((c, idx) =>
                new ColumnDescriptor(c.Name, c.Type, c.IsNullable, ordinal: idx, vectorDimension: c.VectorDimension)).ToList();

            var schema = new TableSchema(createTable.TableName, cols, createTable.PrimaryKeyColumn);
            _catalog.CreateTable(schema, createTable.PrimaryKeyColumn);
            _memTables[schema.TableName] = new MemTable();

            sw.Stop();
            return new QueryResult(schema, new List<SlateValue[]>(), 0, sw.Elapsed.TotalMilliseconds);
        }
    }

    private QueryResult ExecuteCreateIndex(CreateIndexStatement createIndex, Stopwatch sw)
    {
        lock (_lock)
        {
            var descriptor = new IndexDescriptor(createIndex.IndexName, createIndex.IndexType, createIndex.ColumnNames.ToArray(), createIndex.Properties);
            _catalog.AddIndex(createIndex.TableName, descriptor);

            if (createIndex.IndexType == IndexType.HnswVector)
            {
                var tableMeta = _catalog.GetTable(createIndex.TableName)!;
                var col = tableMeta.Schema[createIndex.ColumnNames[0]];
                var hnsw = new HnswIndex(dimension: col.VectorDimension > 0 ? col.VectorDimension : 128);
                _hnswIndexes[$"{createIndex.TableName}.{createIndex.IndexName}"] = hnsw;
            }
            else if (createIndex.IndexType == IndexType.BPlusTree)
            {
                var btree = new BPlusTree<SlateValue, SlateValue[]>();
                _bTrees[$"{createIndex.TableName}.{createIndex.IndexName}"] = btree;
            }

            sw.Stop();
            return new QueryResult(
                new TableSchema("result", new[] { new ColumnDescriptor("index_created", DataType.String) }),
                new List<SlateValue[]> { new[] { SlateValue.FromString(createIndex.IndexName) } },
                0,
                sw.Elapsed.TotalMilliseconds);
        }
    }

    private QueryResult ExecuteDropTable(DropTableStatement dropTable, Stopwatch sw)
    {
        lock (_lock)
        {
            _catalog.DropTable(dropTable.TableName);
            _memTables.Remove(dropTable.TableName);
            sw.Stop();
            return new QueryResult(
                new TableSchema("result", new[] { new ColumnDescriptor("table_dropped", DataType.String) }),
                new List<SlateValue[]> { new[] { SlateValue.FromString(dropTable.TableName) } },
                0,
                sw.Elapsed.TotalMilliseconds);
        }
    }

    private QueryResult ExecuteInsert(InsertStatement insert, TransactionContext? externalTx, Stopwatch sw)
    {
        bool isImplicitTx = externalTx == null;
        var tx = externalTx ?? _mvcc.BeginTransaction();

        try
        {
            var meta = _catalog.GetTable(insert.TableName)!;
            var memTable = _memTables[insert.TableName];
            int pkOrdinal = meta.Schema.PrimaryKeyOrdinal >= 0 ? meta.Schema.PrimaryKeyOrdinal : 0;
            int insertedCount = 0;

            foreach (var rowExprs in insert.Rows)
            {
                var rowVals = new SlateValue[meta.Schema.ColumnCount];
                for (int i = 0; i < meta.Schema.ColumnCount; i++)
                {
                    if (i < rowExprs.Count && rowExprs[i] is LiteralExpression lit)
                    {
                        rowVals[i] = lit.Value;
                    }
                    else
                    {
                        rowVals[i] = SlateValue.Null;
                    }
                }

                var pk = rowVals[pkOrdinal];

                // Append to WAL
                _walWriter.Append(tx.TxId, WalRecordType.Insert, insert.TableName, pk, rowVals);

                // Insert into MemTable
                memTable.Put(pk, rowVals, tx.TxId, isImplicitTx ? tx.ReadTimestamp : ulong.MaxValue);

                // Record in MVCC version store
                _mvcc.RecordVersion(insert.TableName, pk, SlateValue.FromString(pk.AsString()), tx);

                // Update HNSW index if present
                foreach (var idx in meta.Indexes.Values)
                {
                    if (idx.Type == IndexType.HnswVector && _hnswIndexes.TryGetValue($"{insert.TableName}.{idx.Name}", out var hnsw))
                    {
                        int vecOrd = meta.Schema[idx.ColumnNames[0]].Ordinal;
                        if (!rowVals[vecOrd].IsNull)
                        {
                            hnsw.Insert((int)pk.AsInt64(), rowVals[vecOrd].AsVector());
                        }
                    }
                    else if (idx.Type == IndexType.BPlusTree && _bTrees.TryGetValue($"{insert.TableName}.{idx.Name}", out var btree))
                    {
                        int keyOrd = meta.Schema[idx.ColumnNames[0]].Ordinal;
                        if (!rowVals[keyOrd].IsNull)
                        {
                            btree.Insert(rowVals[keyOrd], rowVals);
                        }
                    }
                }

                insertedCount++;
            }

            if (isImplicitTx)
            {
                _mvcc.CommitTransaction(tx, _walWriter);
            }

            sw.Stop();
            return new QueryResult(
                new TableSchema("insert_result", new[] { new ColumnDescriptor("rows_inserted", DataType.Int64) }),
                new List<SlateValue[]> { new[] { SlateValue.FromInt64(insertedCount) } },
                insertedCount,
                sw.Elapsed.TotalMilliseconds);
        }
        catch
        {
            if (isImplicitTx)
            {
                _mvcc.AbortTransaction(tx, _walWriter);
            }
            throw;
        }
    }

    private QueryResult ExecuteSelect(SelectStatement select, TransactionContext? externalTx, Stopwatch sw)
    {
        var tx = externalTx ?? _mvcc.BeginTransaction();
        ulong readTimestamp = tx.ReadTimestamp;

        var planner = new LogicalPlanner(_catalog);
        var logicalPlan = planner.Plan(select);

        // Convert Logical Plan to Physical Plan
        var physOp = BuildPhysicalPlan(logicalPlan, readTimestamp);

        var execCtx = new QueryExecutionContext(tx, readTimestamp);
        physOp.Open(execCtx);

        var rows = new List<SlateValue[]>();
        try
        {
            while (true)
            {
                var batch = physOp.Next();
                if (batch == null) break;

                for (int r = 0; r < batch.SelectedCount; r++)
                {
                    int actualRow = batch.SelectionVector != null ? batch.SelectionVector[r] : r;
                    var row = new SlateValue[physOp.Schema.ColumnCount];
                    for (int c = 0; c < physOp.Schema.ColumnCount; c++)
                    {
                        row[c] = batch.Columns[c].GetValue(actualRow);
                    }
                    rows.Add(row);
                }
            }
        }
        finally
        {
            physOp.Close();
        }

        sw.Stop();
        return new QueryResult(physOp.Schema, rows, rows.Count, sw.Elapsed.TotalMilliseconds, logicalPlan.Explain());
    }

    private QueryResult ExecuteExplain(StatementNode target, Stopwatch sw)
    {
        if (target is SelectStatement select)
        {
            var planner = new LogicalPlanner(_catalog);
            var plan = planner.Plan(select);
            string explain = plan.Explain();
            sw.Stop();
            return new QueryResult(
                new TableSchema("explain", new[] { new ColumnDescriptor("plan", DataType.String) }),
                new List<SlateValue[]> { new[] { SlateValue.FromString(explain) } },
                0,
                sw.Elapsed.TotalMilliseconds,
                explain);
        }

        sw.Stop();
        return new QueryResult(
            new TableSchema("explain", new[] { new ColumnDescriptor("plan", DataType.String) }),
            new List<SlateValue[]> { new[] { SlateValue.FromString(target.ToString() ?? "") } },
            0,
            sw.Elapsed.TotalMilliseconds);
    }

    private IPhysicalOperator BuildPhysicalPlan(LogicalPlanNode node, ulong readTimestamp)
    {
        switch (node)
        {
            case LogicalScan scan:
                var memTable = _memTables[scan.TableName];
                var vectorBatch = memTable.FlushToVectorBatch(scan.Schema, readTimestamp);
                var batches = new List<VectorBatch> { vectorBatch };
                return new TableScanOperator(scan.Schema, batches, scan.ProjectedColumns, scan.PushedPredicate);

            case LogicalFilter filter:
                var childScan = BuildPhysicalPlan(filter.Input, readTimestamp);
                return new FilterOperator(childScan, filter.Predicate);

            case LogicalProject proj:
                var childProj = BuildPhysicalPlan(proj.Input, readTimestamp);
                return new ProjectOperator(childProj, proj.Projections, proj.ProjectedSchema);

            case LogicalSort sort:
                var childSort = BuildPhysicalPlan(sort.Input, readTimestamp);
                return new SortOperator(childSort, sort.OrderBy);

            case LogicalLimit limit:
                var childLimit = BuildPhysicalPlan(limit.Input, readTimestamp);
                return new SortOperator(childLimit, new List<OrderByClause>(), limit.Limit);

            case LogicalVectorKnn knn:
                string indexKey = $"{knn.Input.OutputSchema.TableName}.idx_{knn.VectorColumn}";
                // Check if HNSW index exists
                var matchedHnsw = _hnswIndexes.FirstOrDefault(k => k.Key.StartsWith(knn.Input.OutputSchema.TableName, StringComparison.OrdinalIgnoreCase));
                if (matchedHnsw.Value != null)
                {
                    return new VectorKnnOperator(matchedHnsw.Value, knn.Input.OutputSchema, knn.VectorColumn, knn.Metric, knn.TargetVector, knn.K);
                }
                var childKnn = BuildPhysicalPlan(knn.Input, readTimestamp);
                return new VectorKnnOperator(childKnn, knn.VectorColumn, knn.Metric, knn.TargetVector, knn.K);

            default:
                throw new NotSupportedException($"Unsupported logical plan node: {node.GetType().Name}");
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _walWriter.Flush();
        }
    }

    public void Dispose()
    {
        Flush();
        _walWriter.Dispose();
        _walStream.Dispose();
    }
}
