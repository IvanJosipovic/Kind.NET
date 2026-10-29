using Kind.NET.Scenarios;
using Xunit;

namespace Kind.NET.Tests.E2E.AOT;

public sealed class KindClientAotE2ETests
{
    [Fact]
    public void PublicApiScenariosPass()
    {
        var clusterLifecycle = string.Equals(Environment.GetEnvironmentVariable("KIND_TEST_CLUSTER"), "true", StringComparison.OrdinalIgnoreCase);
        KindClientScenarios.RunAll(new KindClient(), clusterLifecycle);
    }
}
