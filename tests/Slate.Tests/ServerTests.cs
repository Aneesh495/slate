using System.Net.Http.Json;
using System.Text.Json;
using Slate.Engine.Kernel;
using Slate.Server;
using Xunit;

namespace Slate.Tests;

public sealed class ServerTests
{
    private static int GetAvailablePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [Fact]
    public async Task HttpServer_Endpoints_HandleStatusAndQueries()
    {
        int port = GetAvailablePort();
        using var db = new SlateDatabase();
        using var server = new HttpServer(db, port);
        server.Start();

        using var client = new HttpClient();
        string baseUri = $"http://127.0.0.1:{port}";

        // 1. GET /v1/status
        var statusResp = await client.GetAsync($"{baseUri}/v1/status");
        Assert.True(statusResp.IsSuccessStatusCode);
        string statusJson = await statusResp.Content.ReadAsStringAsync();
        Assert.Contains("healthy", statusJson);

        // 2. POST /v1/query (Create table)
        var createResp = await client.PostAsJsonAsync($"{baseUri}/v1/query", new { sql = "CREATE TABLE metrics (id INT PRIMARY KEY, name STRING);" });
        Assert.True(createResp.IsSuccessStatusCode);

        // 3. POST /v1/query (Insert row)
        var insertResp = await client.PostAsJsonAsync($"{baseUri}/v1/query", new { sql = "INSERT INTO metrics VALUES (1, 'CPU_Usage');" });
        Assert.True(insertResp.IsSuccessStatusCode);

        // 4. POST /v1/query (Select row)
        var selectResp = await client.PostAsJsonAsync($"{baseUri}/v1/query", new { sql = "SELECT * FROM metrics;" });
        Assert.True(selectResp.IsSuccessStatusCode);
        string selectJson = await selectResp.Content.ReadAsStringAsync();
        Assert.Contains("CPU_Usage", selectJson);

        // 5. GET /v1/catalog
        var catalogResp = await client.GetAsync($"{baseUri}/v1/catalog");
        Assert.True(catalogResp.IsSuccessStatusCode);
        string catalogJson = await catalogResp.Content.ReadAsStringAsync();
        Assert.Contains("metrics", catalogJson);

        // 6. Transaction endpoints: /v1/tx/begin and /v1/tx/commit
        var txBeginResp = await client.PostAsync($"{baseUri}/v1/tx/begin", null);
        Assert.True(txBeginResp.IsSuccessStatusCode);
        using var txDoc = JsonDocument.Parse(await txBeginResp.Content.ReadAsStringAsync());
        ulong txId = txDoc.RootElement.GetProperty("tx_id").GetUInt64();

        var txCommitResp = await client.PostAsJsonAsync($"{baseUri}/v1/tx/commit", new { tx_id = txId });
        Assert.True(txCommitResp.IsSuccessStatusCode);

        server.Stop();
    }
}
