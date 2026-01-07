using Slate.Engine.Kernel;
using Slate.Server;

int port = 8080;
if (args.Length > 0 && int.TryParse(args[0], out int p))
{
    port = p;
}

using var db = new SlateDatabase();
using var server = new HttpServer(db, port);

server.Start();
Console.WriteLine($"Slate Server listening on http://127.0.0.1:{port}/");
Console.WriteLine("Press Ctrl+C to terminate.");

var tcs = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    tcs.TrySetResult();
};

await tcs.Task;
server.Stop();
Console.WriteLine("Slate Server stopped.");
