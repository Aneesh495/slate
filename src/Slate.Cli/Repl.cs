using System.Text;
using Slate.Engine.Kernel;

namespace Slate.Cli;

/// <summary>
/// Interactive Read-Eval-Print Loop (REPL) for Slate HTAP database.
/// </summary>
public sealed class Repl
{
    private readonly SlateDatabase _database;
    private bool _showTiming = true;
    private bool _running;

    public Repl(SlateDatabase database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public void Run()
    {
        PrintBanner();
        _running = true;
        var buffer = new StringBuilder();

        while (_running)
        {
            string prompt = buffer.Length == 0 ? "slate> " : "   -> ";
            Console.Write(prompt);
            string? line = Console.ReadLine();

            if (line == null) // EOF / Ctrl+D
                break;

            string trimmed = line.Trim();
            if (buffer.Length == 0 && trimmed.StartsWith('.'))
            {
                HandleDotCommand(trimmed);
                continue;
            }

            buffer.AppendLine(line);
            if (trimmed.EndsWith(';'))
            {
                string sql = buffer.ToString().Trim();
                buffer.Clear();

                if (!string.IsNullOrWhiteSpace(sql))
                {
                    ExecuteSql(sql);
                }
            }
        }

        Console.WriteLine("\nGoodbye.");
    }

    private void ExecuteSql(string sql)
    {
        try
        {
            var result = _database.Execute(sql);
            if (result.Rows.Count > 0 || result.Schema.ColumnCount > 0)
            {
                Console.WriteLine(TableFormatter.FormatResult(result, _showTiming));
            }
            else
            {
                string timing = _showTiming ? $" ({result.ExecutionTimeMs:F2} ms)" : "";
                Console.WriteLine($"OK. Rows affected: {result.RowsAffected}{timing}");
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }

    private void HandleDotCommand(string cmd)
    {
        var parts = cmd.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        string name = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : string.Empty;

        switch (name)
        {
            case ".help":
                PrintHelp();
                break;

            case ".tables":
                var tables = _database.Catalog.ListTables();
                if (tables.Count == 0)
                {
                    Console.WriteLine("No tables found in catalog.");
                }
                else
                {
                    Console.WriteLine("Tables:");
                    foreach (var tbl in tables)
                    {
                        Console.WriteLine($"  - {tbl}");
                    }
                }
                break;

            case ".schema":
                if (string.IsNullOrEmpty(arg))
                {
                    foreach (var tbl in _database.Catalog.ListTables())
                    {
                        PrintTableSchema(tbl);
                    }
                }
                else
                {
                    PrintTableSchema(arg);
                }
                break;

            case ".timing":
                if (arg.Equals("on", StringComparison.OrdinalIgnoreCase)) _showTiming = true;
                else if (arg.Equals("off", StringComparison.OrdinalIgnoreCase)) _showTiming = false;
                else _showTiming = !_showTiming;
                Console.WriteLine($"Timing is now {(_showTiming ? "ON" : "OFF")}.");
                break;

            case ".exit":
            case ".quit":
                _running = false;
                break;

            default:
                Console.WriteLine($"Unknown dot-command '{cmd}'. Type .help for a list of commands.");
                break;
        }
    }

    private void PrintTableSchema(string tableName)
    {
        var meta = _database.Catalog.GetTable(tableName);
        if (meta == null)
        {
            Console.WriteLine($"Table '{tableName}' not found.");
            return;
        }

        Console.WriteLine($"\nTable '{meta.Schema.TableName}' (Version {meta.SchemaVersion}, PK: {meta.PrimaryKeyColumn}):");
        foreach (var col in meta.Schema.Columns)
        {
            string nullStr = col.IsNullable ? "NULL" : "NOT NULL";
            string dimStr = col.VectorDimension > 0 ? $"({col.VectorDimension})" : "";
            Console.WriteLine($"  {col.Name.PadRight(16)} {col.Type}{dimStr} {nullStr}");
        }

        var indexes = _database.Catalog.GetIndexes(tableName);
        if (indexes.Count > 0)
        {
            Console.WriteLine("Indexes:");
            foreach (var idx in indexes)
            {
                Console.WriteLine($"  {idx.Name} USING {idx.Type} ({string.Join(", ", idx.ColumnNames)})");
            }
        }
    }

    private static void PrintBanner()
    {
        Console.WriteLine(@"
   _____ _       _______ ______ 
  / ____| |   /\|__   __|  ____|
 | (___ | |  /  \  | |  | |__   
  \___ \| | / /\ \ | |  |  __|  
  ____) | |/ ____ \| |  | |____ 
 |_____/|_/_/    \_\_|  |______|
");
        Console.WriteLine("Slate HTAP Database Engine (v1.0.0)");
        Console.WriteLine("Enter XQL statements ending with ';' or '.help' for commands.\n");
    }

    private static void PrintHelp()
    {
        Console.WriteLine(@"
Available Dot-Commands:
  .help             Show this help menu
  .tables           List all tables in catalog
  .schema [table]   Show table schema and indexes
  .timing [on|off]  Toggle execution timer
  .exit / .quit     Exit the REPL

Example XQL Queries:
  CREATE TABLE docs (id INT PRIMARY KEY, title STRING, emb VECTOR(4));
  CREATE INDEX idx_emb ON docs USING HNSW (emb);
  INSERT INTO docs VALUES (1, 'Hello', [0.1, 0.2, 0.3, 0.4]);
  SELECT * FROM docs WHERE id = 1;
  SELECT id, title FROM docs ORDER BY emb <-> [0.1, 0.2, 0.3, 0.4] LIMIT 5;
");
    }
}
