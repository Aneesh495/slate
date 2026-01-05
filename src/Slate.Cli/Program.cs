using Slate.Cli;
using Slate.Engine.Kernel;

if (args.Length >= 1 && (args[0] == "--demo" || args[0] == "demo"))
{
    DemoScenario.Run();
    return;
}

using var db = new SlateDatabase();

if (args.Length >= 2 && args[0] == "-e")
{
    string query = args[1];
    var res = db.Execute(query);
    Console.WriteLine(TableFormatter.FormatResult(res));
    return;
}

var repl = new Repl(db);
repl.Run();
