using Slate.Core.Primitives;
using Slate.Engine.Kernel;
using Xunit;

namespace Slate.Tests;

public sealed class EngineTests
{
    [Fact]
    public void SlateDatabase_EndToEndHtap_ExecutesRelationalAndVectorQueries()
    {
        using var db = new SlateDatabase();

        // 1. Create table
        var createRes = db.Execute("CREATE TABLE documents (id INT PRIMARY KEY, title STRING, embedding VECTOR(4));");
        Assert.NotNull(createRes);
        Assert.True(db.Catalog.TableExists("documents"));

        // 2. Create HNSW index
        var indexRes = db.Execute("CREATE INDEX idx_emb ON documents USING HNSW (embedding);");
        Assert.NotNull(indexRes);

        // 3. Insert records
        var insertRes = db.Execute(@"
            INSERT INTO documents VALUES 
            (1, 'Database Systems', [1.0, 0.0, 0.0, 0.0]),
            (2, 'Machine Learning', [0.0, 1.0, 0.0, 0.0]),
            (3, 'Vector Search', [0.8, 0.2, 0.0, 0.0]);
        ");
        Assert.Equal(3, insertRes.RowsAffected);

        // 4. Relational query: SELECT id, title FROM documents WHERE id = 1;
        var selectRes = db.Execute("SELECT id, title FROM documents WHERE id = 1;");
        Assert.Single(selectRes.Rows);
        Assert.Equal(1L, selectRes.Rows[0][0].AsInt64());
        Assert.Equal("Database Systems", selectRes.Rows[0][1].AsString());

        // 5. Vector KNN search: ORDER BY embedding <-> [0.9, 0.1, 0.0, 0.0] LIMIT 1
        var knnRes = db.Execute("SELECT id, title FROM documents ORDER BY embedding <-> [0.9, 0.1, 0.0, 0.0] LIMIT 1;");
        Assert.Single(knnRes.Rows);
        // Nearest is ID 1 (Database Systems)
        Assert.Equal(1L, knnRes.Rows[0][0].AsInt64());

        // 6. EXPLAIN verification
        var explainRes = db.Execute("EXPLAIN SELECT id FROM documents WHERE id = 2;");
        Assert.NotNull(explainRes.ExplainPlan);
        Assert.Contains("LogicalScan", explainRes.ExplainPlan);
    }

    [Fact]
    public void SlateDatabase_TransactionRollback_DiscardsAbortedWrites()
    {
        using var db = new SlateDatabase();
        db.Execute("CREATE TABLE accounts (id INT PRIMARY KEY, balance INT);");

        // Transaction 1: Committed
        var tx1 = db.BeginTransaction();
        db.Execute("INSERT INTO accounts VALUES (101, 500);", tx1);
        db.CommitTransaction(tx1);

        // Verify account 101 exists
        var res1 = db.Execute("SELECT id, balance FROM accounts WHERE id = 101;");
        Assert.Single(res1.Rows);

        // Transaction 2: Aborted
        var tx2 = db.BeginTransaction();
        db.Execute("INSERT INTO accounts VALUES (102, 900);", tx2);
        db.AbortTransaction(tx2);

        // Verify account 102 was rolled back
        // In MVCC / ARIES rollback, aborted transaction versions are excluded
        var res2 = db.Execute("SELECT id, balance FROM accounts WHERE id = 102;");
        Assert.Empty(res2.Rows);
    }
}
