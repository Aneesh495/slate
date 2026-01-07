using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Slate.Core.Primitives;
using Slate.Core.Transactions;
using Slate.Engine.Kernel;

namespace Slate.Server;

public sealed class HttpServer : IDisposable
{
    private readonly SlateDatabase _database;
    private readonly int _port;
    private readonly HttpListener _listener;
    private readonly ConcurrentDictionary<ulong, TransactionContext> _activeTransactions = new();
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    public int Port => _port;
    public bool IsRunning => _listener.IsListening;

    public HttpServer(SlateDatabase database, int port = 8080)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
    }

    public void Start()
    {
        if (_listener.IsListening) return;

        _listener.Start();
        _cts = new CancellationTokenSource();
        _listenTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        if (!_listener.IsListening) return;

        _cts?.Cancel();
        _listener.Stop();
        try
        {
            _listenTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore cancellation / task abort exceptions on shutdown
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(() => HandleRequestAsync(context), ct);
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                if (ct.IsCancellationRequested) break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var req = context.Request;
        var resp = context.Response;

        // CORS headers
        resp.Headers.Add("Access-Control-Allow-Origin", "*");
        resp.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        resp.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (req.HttpMethod == "OPTIONS")
        {
            resp.StatusCode = (int)HttpStatusCode.NoContent;
            resp.Close();
            return;
        }

        try
        {
            string path = req.Url?.AbsolutePath ?? "/";

            if (req.HttpMethod == "POST" && path == "/v1/query")
            {
                await HandleQueryEndpointAsync(req, resp).ConfigureAwait(false);
            }
            else if (req.HttpMethod == "GET" && path == "/v1/status")
            {
                await HandleStatusEndpointAsync(resp).ConfigureAwait(false);
            }
            else if (req.HttpMethod == "GET" && path == "/v1/catalog")
            {
                await HandleCatalogEndpointAsync(resp).ConfigureAwait(false);
            }
            else if (req.HttpMethod == "POST" && path == "/v1/tx/begin")
            {
                HandleTxBegin(resp);
            }
            else if (req.HttpMethod == "POST" && path == "/v1/tx/commit")
            {
                await HandleTxCommitAsync(req, resp).ConfigureAwait(false);
            }
            else if (req.HttpMethod == "POST" && path == "/v1/tx/abort")
            {
                await HandleTxAbortAsync(req, resp).ConfigureAwait(false);
            }
            else
            {
                resp.StatusCode = (int)HttpStatusCode.NotFound;
                await WriteJsonAsync(resp, new { error = $"Endpoint '{path}' not found." }).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            resp.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteJsonAsync(resp, new { error = ex.Message }).ConfigureAwait(false);
        }
        finally
        {
            try { resp.Close(); } catch { }
        }
    }

    private async Task HandleQueryEndpointAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        string body = await reader.ReadToEndAsync().ConfigureAwait(false);

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("sql", out var sqlProp))
        {
            resp.StatusCode = (int)HttpStatusCode.BadRequest;
            await WriteJsonAsync(resp, new { error = "Missing 'sql' field in request body." }).ConfigureAwait(false);
            return;
        }

        string sql = sqlProp.GetString() ?? string.Empty;

        TransactionContext? tx = null;
        if (doc.RootElement.TryGetProperty("tx_id", out var txProp) && txProp.ValueKind == JsonValueKind.Number)
        {
            ulong txId = txProp.GetUInt64();
            _activeTransactions.TryGetValue(txId, out tx);
        }

        var result = _database.Execute(sql, tx);

        // Serialize query result
        var colNames = result.Schema.Columns.Select(c => c.Name).ToArray();
        var rowObjects = new List<object?[]>(result.Rows.Count);

        foreach (var r in result.Rows)
        {
            var row = new object?[r.Length];
            for (int i = 0; i < r.Length; i++)
            {
                var val = r[i];
                row[i] = val.IsNull ? null : (val.Type == DataType.VectorFloat32 ? val.AsVector() : val.AsString());
            }
            rowObjects.Add(row);
        }

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonAsync(resp, new
        {
            columns = colNames,
            rows = rowObjects,
            rows_affected = result.RowsAffected,
            execution_time_ms = result.ExecutionTimeMs,
            explain = result.ExplainPlan
        }).ConfigureAwait(false);
    }

    private async Task HandleStatusEndpointAsync(HttpListenerResponse resp)
    {
        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonAsync(resp, new
        {
            status = "healthy",
            version = "1.0.0",
            tables_count = _database.Catalog.ListTables().Count,
            active_transactions = _activeTransactions.Count,
            current_timestamp = _database.Mvcc.CurrentTimestamp,
            memory_bytes = GC.GetTotalMemory(false)
        }).ConfigureAwait(false);
    }

    private async Task HandleCatalogEndpointAsync(HttpListenerResponse resp)
    {
        var tables = new List<object>();
        foreach (var tblName in _database.Catalog.ListTables())
        {
            var meta = _database.Catalog.GetTable(tblName)!;
            tables.Add(new
            {
                table_name = meta.Schema.TableName,
                primary_key = meta.PrimaryKeyColumn,
                version = meta.SchemaVersion,
                columns = meta.Schema.Columns.Select(c => new
                {
                    name = c.Name,
                    type = c.Type.ToString(),
                    is_nullable = c.IsNullable,
                    vector_dim = c.VectorDimension
                }),
                indexes = meta.Indexes.Values.Select(i => new
                {
                    name = i.Name,
                    type = i.Type.ToString(),
                    columns = i.ColumnNames
                })
            });
        }

        resp.StatusCode = (int)HttpStatusCode.OK;
        await WriteJsonAsync(resp, new { tables }).ConfigureAwait(false);
    }

    private void HandleTxBegin(HttpListenerResponse resp)
    {
        var tx = _database.BeginTransaction();
        _activeTransactions[tx.TxId] = tx;

        resp.StatusCode = (int)HttpStatusCode.OK;
        WriteJson(resp, new
        {
            tx_id = tx.TxId,
            read_timestamp = tx.ReadTimestamp,
            isolation = tx.Isolation.ToString()
        });
    }

    private async Task HandleTxCommitAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        string body = await reader.ReadToEndAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        ulong txId = doc.RootElement.GetProperty("tx_id").GetUInt64();
        if (_activeTransactions.TryRemove(txId, out var tx))
        {
            _database.CommitTransaction(tx);
            resp.StatusCode = (int)HttpStatusCode.OK;
            await WriteJsonAsync(resp, new { tx_id = txId, status = "committed" }).ConfigureAwait(false);
        }
        else
        {
            resp.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(resp, new { error = $"Transaction {txId} not found or already closed." }).ConfigureAwait(false);
        }
    }

    private async Task HandleTxAbortAsync(HttpListenerRequest req, HttpListenerResponse resp)
    {
        using var reader = new StreamReader(req.InputStream, req.ContentEncoding);
        string body = await reader.ReadToEndAsync().ConfigureAwait(false);
        using var doc = JsonDocument.Parse(body);

        ulong txId = doc.RootElement.GetProperty("tx_id").GetUInt64();
        if (_activeTransactions.TryRemove(txId, out var tx))
        {
            _database.AbortTransaction(tx);
            resp.StatusCode = (int)HttpStatusCode.OK;
            await WriteJsonAsync(resp, new { tx_id = txId, status = "aborted" }).ConfigureAwait(false);
        }
        else
        {
            resp.StatusCode = (int)HttpStatusCode.NotFound;
            await WriteJsonAsync(resp, new { error = $"Transaction {txId} not found." }).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonAsync(HttpListenerResponse resp, object payload)
    {
        resp.ContentType = "application/json; charset=utf-8";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        resp.ContentLength64 = bytes.Length;
        await resp.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
    }

    private static void WriteJson(HttpListenerResponse resp, object payload)
    {
        resp.ContentType = "application/json; charset=utf-8";
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        resp.ContentLength64 = bytes.Length;
        resp.OutputStream.Write(bytes);
    }

    public void Dispose()
    {
        Stop();
        ((IDisposable)_listener).Dispose();
    }
}
