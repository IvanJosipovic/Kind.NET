namespace Kind.NET.Tests;

using System.Runtime.InteropServices;

public sealed class KindClientTests
{
    private static string[] Args(params string[] values) => values;
    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private static string ShellPath => IsWindows
        ? Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe"
        : "/bin/sh";

    private static string[] ShellArguments(string command) => IsWindows
        ? Args("/c", command)
        : Args("-c", command);
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
        (await client.GetNodesAsync("demo")).ShouldBe(Args("clusters"));
        (await client.GetAllNodesAsync()).ShouldBe(Args("clusters"));
        (await client.GetKubeConfigAsync("demo")).ShouldBe("clusters\n");
        await client.ExportKubeConfigAsync("demo");
        await client.ExportLogsAsync("demo");
        await client.LoadDockerImagesAsync("demo", Args("app:latest"));
        await client.LoadImageArchiveAsync("demo", "images.tar");
        await client.BuildNodeImageAsync(new KindBuildNodeImageOptions("source"));
        (await client.GetVersionAsync()).ShouldBe("clusters\n");
        (await client.GenerateCompletionAsync("powershell")).ShouldBe("clusters\n");

        calls.Count.ShouldBe(13);
        calls[0].ShouldBe(Args("create", "cluster", "--name", "demo"));
        calls[1].ShouldBe(Args("delete", "cluster", "--name", "demo"));
        calls[2].ShouldBe(Args("get", "clusters"));
        calls[3].ShouldBe(Args("get", "nodes", "--name", "demo"));
        calls[4].ShouldBe(Args("get", "nodes", "--all-clusters"));
        calls[5].ShouldBe(Args("get", "kubeconfig", "--name", "demo"));
        calls[6].ShouldBe(Args("export", "kubeconfig", "--name", "demo"));
        calls[7].ShouldBe(Args("export", "logs", "--name", "demo"));
        calls[8].ShouldBe(Args("load", "docker-image", "app:latest", "--name", "demo"));
        calls[9].ShouldBe(Args("load", "image-archive", "images.tar", "--name", "demo"));
        calls[10].ShouldBe(Args("build", "node-image", "source"));
        calls[11].ShouldBe(Args("version"));
        calls[12].ShouldBe(Args("completion", "powershell"));
    }

    [Fact]
    public async Task DefaultClusterOverloadsBuildExpectedCommands()
    {
        var calls = new List<IReadOnlyList<string>>();
        var client = new KindClient(new KindClientOptions { UseBundledExecutable = false }, (arguments, _) =>
        {
            calls.Add(arguments);
            return Task.FromResult(new KindCommandResult(0, "output", ""));
        });

        await client.GetNodesAsync();
        await client.GetKubeConfigAsync();
        await client.GetInternalKubeConfigAsync();
        await client.ExportKubeConfigAsync();
        await client.ExportInternalKubeConfigAsync();
        await client.ExportDefaultKubeConfigAsync("default.yaml");
        await client.ExportDefaultInternalKubeConfigAsync("default-internal.yaml");
        await client.ExportLogsAsync();
        await client.ExportDefaultLogsAsync("default-logs");

        calls.ShouldBe(new IReadOnlyList<string>[]
        {
            Args("get", "nodes", "--name", "kind"),
            Args("get", "kubeconfig", "--name", "kind"),
            Args("get", "kubeconfig", "--name", "kind", "--internal"),
            Args("export", "kubeconfig", "--name", "kind"),
            Args("export", "kubeconfig", "--name", "kind", "--internal"),
            Args("export", "kubeconfig", "--name", "kind", "--kubeconfig", "default.yaml"),
            Args("export", "kubeconfig", "--name", "kind", "--kubeconfig", "default-internal.yaml", "--internal"),
            Args("export", "logs", "--name", "kind"),
            Args("export", "logs", "default-logs", "--name", "kind")
        });
    }

    [Fact]
    public async Task GenericExecutionPropagatesCommandFailure()
    {
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { UseBundledExecutable = false }, (_, _) =>
            Task.FromException<KindCommandResult>(new KindCommandException("failed"))).ExecuteAsync(Args("version")));
        exception.Message.ShouldBe("failed");
    }

    [Fact]
    public async Task GetInternalKubeConfigRequestsInternalAddress()
    {
        IReadOnlyList<string>? capturedArguments = null;
        var client = new KindClient(new KindClientOptions { UseBundledExecutable = false }, (arguments, _) =>
        {
            capturedArguments = arguments;
            return Task.FromResult(new KindCommandResult(0, "config", ""));
        });

        await client.GetInternalKubeConfigAsync("demo");

        capturedArguments.ShouldBe(Args("get", "kubeconfig", "--name", "demo", "--internal"));
    }

    [Fact]
    public async Task ExecuteAsyncCapturesSuccessfulProcessOutput()
    {
        var result = await new KindClient(new KindClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments("echo hello"));
        result.ExitCode.ShouldBe(0);
        result.StandardOutput!.ShouldContain("hello");
    }

    [Fact]
    public async Task ExecuteAsyncUsesConfiguredWorkingDirectoryAndEnvironment()
    {
        var workingDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workingDirectory);
        try
        {
            var command = IsWindows
                ? "echo %KIND_NET_TEST_VALUE% & cd"
                : "printf '%s\\n' \"$KIND_NET_TEST_VALUE\"; pwd";
            var result = await new KindClient(new KindClientOptions
            {
                ExecutablePath = ShellPath,
                WorkingDirectory = workingDirectory,
                Environment = new Dictionary<string, string?> { ["KIND_NET_TEST_VALUE"] = "environment-value" }
            }).ExecuteAsync(ShellArguments(command));

            result.StandardOutput.ShouldContain("environment-value");
            result.StandardOutput.ShouldContain(workingDirectory);
        }
        finally
        {
            Directory.Delete(workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsyncHonorsConfiguredCommandTimeout()
    {
        var command = IsWindows ? "for /L %i in (0,0,1) do @rem" : "while true; do :; done";
        var exception = await Should.ThrowAsync<KindCommandException>(() =>
            new KindClient(new KindClientOptions
            {
                ExecutablePath = ShellPath,
                CommandTimeout = TimeSpan.FromMilliseconds(100)
            }).ExecuteAsync(ShellArguments(command)));

        exception.Message.ShouldBe("Kind command was cancelled or timed out.");
        exception.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsyncReportsNonzeroExitCodeAndOutput()
    {
        var command = IsWindows ? "echo error 1>&2 & exit 7" : "echo error >&2; exit 7";
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments(command)));
        exception.ExitCode.ShouldBe(7);
        exception.StandardError!.ShouldContain("error");
    }

    [Fact]
    public async Task ExecuteAsyncReportsCancellationAndStopsProcess()
    {
        var command = IsWindows ? "for /L %i in (0,0,1) do @rem" : "while true; do :; done";
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var exception = await Should.ThrowAsync<KindCommandException>(() =>
            new KindClient(new KindClientOptions { ExecutablePath = ShellPath }).ExecuteAsync(ShellArguments(command), cancellation.Token));

        exception.Message.ShouldBe("Kind command was cancelled or timed out.");
        exception.InnerException.ShouldBeAssignableTo<OperationCanceledException>();
    }

    [Fact]
    public async Task ExecuteAsyncReportsUnstartableExecutable()
    {
        var exception = await Should.ThrowAsync<KindCommandException>(() => new KindClient(new KindClientOptions { ExecutablePath = "missing-kind-executable" }).ExecuteAsync(Args("version")));
        exception.InnerException.ShouldNotBeNull();
    }
}
