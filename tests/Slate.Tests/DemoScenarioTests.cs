using Slate.Cli;
using Xunit;

namespace Slate.Tests;

public sealed class DemoScenarioTests
{
    [Fact]
    public void DemoScenario_ExecutesCleanly_AllMilestonesSucceed()
    {
        // Executes full DDL, ingestion, MVCC rollback, OLAP aggregations, HNSW KNN, and hybrid search
        DemoScenario.Run(silent: true);
    }
}
