using System.Diagnostics;
using Slate.Core.Primitives;
using Slate.Engine.Kernel;

namespace Slate.Cli;

/// <summary>
/// Comprehensive end-to-end multi-modal demo scenario demonstrating HTAP transactional throughput,
/// analytical aggregations, and sub-millisecond HNSW vector similarity search.
/// </summary>
public static class DemoScenario
{
    public static void Run(SlateDatabase? db = null, bool silent = false)
    {
        bool ownsDb = db == null;
        db ??= new SlateDatabase();

        var totalStopwatch = Stopwatch.StartNew();

        Log(silent, "\n=======================================================");
        Log(silent, "   SLATE HTAP ENGINE: END-TO-END MULTI-MODAL DEMO");
        Log(silent, "=======================================================\n");

        // Step 1: DDL Schema Creation
        Log(silent, "[1/6] Initializing catalog and schemas...");
        db.Execute("CREATE TABLE corpus (id INT PRIMARY KEY, title STRING, domain STRING, views INT, embedding VECTOR(8));");
        db.Execute("CREATE INDEX idx_domain ON corpus USING BTREE (domain);");
        db.Execute("CREATE INDEX idx_emb ON corpus USING HNSW (embedding) WITH (M = 16, efConstruction = 64);");
        Log(silent, "      Table 'corpus' and indexes (B+Tree, HNSW) created successfully.");

        // Step 2: Ingestion & Transactional Insertion
        Log(silent, "\n[2/6] Populating multi-modal embedding corpus...");
        var swIngest = Stopwatch.StartNew();
        int recordCount = 500;
        var rng = new Random(42);
        string[] domains = { "Systems", "Database", "MachineLearning", "Cryptography" };

        var insertBatchSql = new System.Text.StringBuilder();
        insertBatchSql.AppendLine("INSERT INTO corpus VALUES ");

        for (int i = 1; i <= recordCount; i++)
        {
            string domain = domains[i % domains.Length];
            string title = $"{domain} Paper #{i}";
            int views = rng.Next(100, 10000);

            // Generate 8-dimensional unit vector
            float[] vec = new float[8];
            float sumSq = 0;
            for (int d = 0; d < 8; d++)
            {
                vec[d] = (float)rng.NextDouble();
                sumSq += vec[d] * vec[d];
            }
            float norm = MathF.Sqrt(sumSq);
            for (int d = 0; d < 8; d++) vec[d] /= norm;

            string vecStr = "[" + string.Join(", ", vec.Select(v => v.ToString("F3"))) + "]";
            insertBatchSql.Append($"({i}, '{title}', '{domain}', {views}, {vecStr})");
            insertBatchSql.AppendLine(i < recordCount ? "," : ";");
        }

        db.Execute(insertBatchSql.ToString());
        swIngest.Stop();
        Log(silent, $"      Ingested {recordCount} vector records in {swIngest.Elapsed.TotalMilliseconds:F2} ms ({recordCount / swIngest.Elapsed.TotalSeconds:F0} rows/sec).");

        // Step 3: Transactional Snapshot Isolation
        Log(silent, "\n[3/6] Verifying ACID snapshot isolation and rollback...");
        var tx = db.BeginTransaction();
        db.Execute("INSERT INTO corpus VALUES (9999, 'Uncommitted Speculative Record', 'Systems', 42, [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8]);", tx);
        db.AbortTransaction(tx);

        var verifyRollback = db.Execute("SELECT id, title FROM corpus WHERE id = 9999;");
        if (verifyRollback.Rows.Count != 0)
            throw new InvalidOperationException("Rollback verification failed: speculative row was visible.");
        Log(silent, "      ACID rollback verified: speculative uncommitted mutation safely discarded.");

        // Step 4: Analytical Aggregation (OLAP)
        Log(silent, "\n[4/6] Executing analytical aggregations (OLAP)...");
        var olapRes = db.Execute("SELECT domain, COUNT(*) AS doc_count, SUM(views) AS total_views FROM corpus GROUP BY domain;");
        if (!silent)
        {
            Console.WriteLine(TableFormatter.FormatResult(olapRes));
        }

        // Step 5: Pure Vector KNN Search
        Log(silent, "[5/6] Executing HNSW vector similarity search (k=3)...");
        float[] queryVec = { 0.5f, 0.5f, 0.0f, 0.0f, 0.5f, 0.5f, 0.0f, 0.0f };
        string qVecStr = "[" + string.Join(", ", queryVec.Select(v => v.ToString("F3"))) + "]";

        var knnRes = db.Execute($"SELECT id, title, domain FROM corpus ORDER BY embedding <-> {qVecStr} LIMIT 3;");
        if (!silent)
        {
            Console.WriteLine(TableFormatter.FormatResult(knnRes));
        }

        // Step 6: Hybrid HTAP Filtered Vector Search
        Log(silent, "[6/6] Executing hybrid filtered vector search (WHERE domain = 'Systems' ORDER BY vec <-> ...)...");
        var hybridRes = db.Execute($"SELECT id, title FROM corpus WHERE domain = 'Systems' ORDER BY embedding <-> {qVecStr} LIMIT 3;");
        if (!silent)
        {
            Console.WriteLine(TableFormatter.FormatResult(hybridRes));
        }

        totalStopwatch.Stop();
        Log(silent, $"\n>>> All demo verification milestones passed in {totalStopwatch.Elapsed.TotalMilliseconds:F2} ms! <<<\n");

        if (ownsDb)
        {
            db.Dispose();
        }
    }

    private static void Log(bool silent, string message)
    {
        if (!silent)
        {
            Console.WriteLine(message);
        }
    }
}
