namespace Kind.NET.Tests;

public sealed class KindArgumentBuilderTests
{
    private static string[] Args(params string[] values) => values;
    private static readonly string[] s_appImages = ["app:one", "db:two"];
    private static readonly string[] s_workerNode = ["worker"];
    private static readonly string[] s_expectedCreate = ["create", "cluster", "--name", "demo", "--image", "kindest/node:v1", "--config", "config path.yaml", "--kubeconfig", "kube config", "--wait", "2m", "--retain"];
    [Fact]
    public void CreateIncludesAllOptions()
    {
        var actual = KindArgumentBuilder.Create(new KindClusterOptions("demo", "kindest/node:v1", "config path.yaml", "kube config", TimeSpan.FromMinutes(2), true));
        actual.ShouldBe(s_expectedCreate);
    }

    [Fact]
    public void LoadDockerImagesPreservesMultipleImagesAndNodes()
    {
        var actual = KindArgumentBuilder.LoadDockerImages("demo", s_appImages, s_workerNode);
        actual.ShouldBe(Args("load", "docker-image", "app:one", "db:two", "--name", "demo", "--nodes", "worker"));
    }

    [Fact]
    public void DeleteIncludesAlternateKubeconfig()
    {
        KindArgumentBuilder.Delete(new KindDeleteClusterOptions("demo", "config path.yaml"))
            .ShouldBe(Args("delete", "cluster", "--name", "demo", "--kubeconfig", "config path.yaml"));
    }

    [Fact]
    public void ExportKubeConfigIncludesPathAndInternalAddress()
    {
        KindArgumentBuilder.ExportKubeConfig("demo", "config path.yaml", internalAddress: true)
            .ShouldBe(Args("export", "kubeconfig", "--name", "demo", "--kubeconfig", "config path.yaml", "--internal"));
    }

    [Fact]
    public void ExportLogsIncludesPathAndClusterName()
    {
        KindArgumentBuilder.ExportLogs("demo", "logs path")
            .ShouldBe(Args("export", "logs", "logs path", "--name", "demo"));
    }

    [Fact]
    public void LoadImageArchiveIncludesSelectedNodes()
    {
        KindArgumentBuilder.LoadImageArchive("demo", "images path.tar", s_workerNode)
            .ShouldBe(Args("load", "image-archive", "images path.tar", "--name", "demo", "--nodes", "worker"));
    }

    [Fact]
    public void BuildNodeImageOmitsUnspecifiedOptions()
    {
        var actual = KindArgumentBuilder.BuildNodeImage(new KindBuildNodeImageOptions("source path", Image: "custom:tag"));
        actual.ShouldBe(Args("build", "node-image", "source path", "--image", "custom:tag"));
    }

    [Fact]
    public void BuildNodeImageIncludesAllOptions()
    {
        KindArgumentBuilder.BuildNodeImage(new KindBuildNodeImageOptions("v1.37.0", "kindest/base:custom", "custom:tag", "release", "arm64"))
            .ShouldBe(Args("build", "node-image", "v1.37.0", "--arch", "arm64", "--base-image", "kindest/base:custom", "--image", "custom:tag", "--type", "release"));
    }
}
