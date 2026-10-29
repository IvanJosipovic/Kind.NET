using Kind.NET.Scenarios;
using Xunit;

namespace Kind.NET.Tests.E2E;

public sealed class KindClientE2ETests
{
    [Fact]
    public void PublicApiScenariosPass()
    {
        var clusterLifecycle = string.Equals(Environment.GetEnvironmentVariable("KIND_TEST_CLUSTER"), "true", StringComparison.OrdinalIgnoreCase);
        KindClientScenarios.RunAll(new KindClient(), clusterLifecycle);
    }
}
