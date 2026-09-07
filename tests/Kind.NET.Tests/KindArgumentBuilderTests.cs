namespace Kind.NET.Tests;

public sealed class KindArgumentBuilderTests
{
    private static string[] Args(params string[] values) => values;
    private static readonly string[] AppImages = { "app:one", "db:two" };
    private static readonly string[] WorkerNode = { "worker" };
    private static readonly string[] ExpectedCreate = { "create", "cluster", "--name", "demo", "--image", "kindest/node:v1", "--config", "config path.yaml", "--kubeconfig", "kube config", "--wait", "2m", "--retain" };
    [Fact]
    public void CreateIncludesAllOptions()
    {
        var actual = KindArgumentBuilder.Create(new KindClusterOptions("demo", "kindest/node:v1", "config path.yaml", "kube config", TimeSpan.FromMinutes(2), true));
        actual.ShouldBe(ExpectedCreate);
    }

    [Fact]
    public void LoadDockerImagesPreservesMultipleImagesAndNodes()
    {
        var actual = KindArgumentBuilder.LoadDockerImages("demo", AppImages, WorkerNode);
        actual.ShouldBe(Args("load", "docker-image", "app:one", "db:two", "--name", "demo", "--nodes", "worker"));
    }

    [Fact]
    public void BuildNodeImageOmitsUnspecifiedOptions()
    {
        var actual = KindArgumentBuilder.BuildNodeImage(new KindBuildNodeImageOptions("source path", Image: "custom:tag"));
        actual.ShouldBe(Args("build", "node-image", "source path", "--image", "custom:tag"));
    }
}
