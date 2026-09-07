namespace Kind.NET.Tests;

public sealed class KindClientTests
{
    private static string[] Args(params string[] values) => values;
    [Fact]
    public async Task TypedOperationsBuildExpectedCommands()
    {
        var calls = new List<IReadOnlyList<string>>();
        var client = new KindClient(new KindClientOptions { UseBundledExecutable = false }, (args, _) =>
        {
            calls.Add(args);
            return Task.FromResult(new KindCommandResult(0, "clusters\n", ""));
        });

        await client.CreateClusterAsync(new KindClusterOptions("demo"));
        await client.DeleteClusterAsync(new KindDeleteClusterOptions("demo"));
        (await client.GetClustersAsync()).ShouldBe(Args("clusters"));
        (await client.GetKubeConfigAsync("demo")).ShouldBe("clusters\n");
        await client.ExportKubeConfigAsync("demo");
        await client.ExportLogsAsync("demo");
        await client.LoadDockerImagesAsync("demo", Args("app:latest"));
        await client.LoadImageArchiveAsync("demo", "images.tar");
        await client.BuildNodeImageAsync(new KindBuildNodeImageOptions("source"));
        (await client.GetVersionAsync()).ShouldBe("clusters\n");
        (await client.GenerateCompletionAsync("powershell")).ShouldBe("clusters\n");

        calls.Count.ShouldBe(11);
        calls[0].ShouldBe(Args("create", "cluster", "--name", "demo"));
        calls[1].ShouldBe(Args("delete", "cluster", "--name", "demo"));
        calls[2].ShouldBe(Args("get", "clusters"));
        calls[3].ShouldBe(Args("get", "kubeconfig", "--name", "demo"));
        calls[4].ShouldBe(Args("export", "kubeconfig", "--name", "demo"));
        calls[5].ShouldBe(Args("export", "logs", "--name", "demo"));
        calls[6].ShouldBe(Args("load", "docker-image", "app:latest", "--name", "demo"));
        calls[7].ShouldBe(Args("load", "image-archive", "images.tar", "--name", "demo"));
        calls[8].ShouldBe(Args("build", "node-image", "source"));
        calls[9].ShouldBe(Args("version"));
        calls[10].ShouldBe(Args("completion", "powershell"));
    }

    [Fact]
    public async Task GenericExecutionPropagatesCommandFailure()
    {
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { UseBundledExecutable = false }, (_, _) =>
            Task.FromException<KindCommandResult>(new KindCommandException("failed"))).ExecuteAsync(Args("version")));
        exception.Message.ShouldBe("failed");
    }

    [Fact]
    public async Task ExecuteAsyncCapturesSuccessfulProcessOutput()
    {
        var result = await new KindClient(new KindClientOptions { ExecutablePath = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" }).ExecuteAsync(Args("/c", "echo", "hello"));
        result.ExitCode.ShouldBe(0);
        result.StandardOutput!.ShouldContain("hello");
    }

    [Fact]
    public async Task ExecuteAsyncReportsNonzeroExitCodeAndOutput()
    {
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { ExecutablePath = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe" }).ExecuteAsync(Args("/c", "echo error 1>&2 & exit 7")));
        exception.ExitCode.ShouldBe(7);
        exception.StandardError!.ShouldContain("error");
    }

    [Fact]
    public async Task ExecuteAsyncReportsUnstartableExecutable()
    {
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { ExecutablePath = "missing-kind-executable" }).ExecuteAsync(Args("version")));
        exception.InnerException.ShouldNotBeNull();
    }
}
