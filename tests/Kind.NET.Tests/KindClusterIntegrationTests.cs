namespace Kind.NET.Tests;

using System.Diagnostics;

public sealed class KindClusterIntegrationTests
{
    [Fact(Timeout = 900_000)]
    [Trait("Category", "Integration")]
    public async Task BuildsReleaseNodeImage()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The Kind release node-image build is validated on Linux; Kind v0.33.0 passes Windows paths to the container as POSIX paths.");
        }

        var cancellationToken = TestContext.Current.CancellationToken;
        var provider = Environment.GetEnvironmentVariable("KIND_EXPERIMENTAL_PROVIDER") ?? "docker";
        var providerError = await CheckProviderAsync(provider);
        if (providerError is not null)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The Kind node-image integration test requires a working {provider} runtime. {providerError}");
            }

            Assert.Skip($"The Kind node-image integration test requires a working {provider} runtime. {providerError}");
        }

        var image = $"kind-net-node:{Guid.NewGuid():N}";
        var client = new KindClient(new KindClientOptions { CommandTimeout = TimeSpan.FromMinutes(12) });
        await client.BuildNodeImageAsync(new KindBuildNodeImageOptions("v1.37.0", Image: image, Type: "release"), cancellationToken);
        (await RunContainerCliAsync(provider, ["image", "inspect", image], cancellationToken)).StandardOutput.ShouldContain(image);
    }

    [Fact(Timeout = 1_500_000)]
    [Trait("Category", "Integration")]
    public async Task CreatesReachableClusterAndDeletesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var provider = Environment.GetEnvironmentVariable("KIND_EXPERIMENTAL_PROVIDER") ?? "docker";
        var providerError = await CheckProviderAsync(provider);
        var kubectlError = await CheckCommandAsync("kubectl", ["version", "--client=true", "--output=json"]);
        if (providerError is not null || kubectlError is not null)
        {
            var message = providerError ?? kubectlError!;
            if (string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"The Kind integration test requires a working {provider} runtime and kubectl. {message}");
            }

            Assert.Skip($"The Kind integration test requires a working {provider} runtime and kubectl. {message}");
        }

        var clusterName = $"kn-{Guid.NewGuid():N}";
        var workspace = Path.Combine(Path.GetTempPath(), $"kind-net-{clusterName}");
        Directory.CreateDirectory(workspace);
        var kubeConfigPath = Path.Combine(workspace, "kubeconfig.yaml");
        var logsPath = Path.Combine(workspace, "logs");
        var archivePath = Path.Combine(workspace, "image.tar");
        var imageContextPath = Path.Combine(workspace, "image");
        Directory.CreateDirectory(imageContextPath);
        var dockerImage = $"kind-net-test:{Guid.NewGuid():N}";
        var archiveImage = $"{dockerImage}-archive";
        var client = new KindClient(new KindClientOptions { CommandTimeout = TimeSpan.FromMinutes(12) });
        var clusterMayExist = false;

        try
        {
            var version = await client.GetVersionAsync(cancellationToken);
            version.ShouldContain("kind v");
            (await client.ExecuteAsync(["--quiet", "version"], cancellationToken)).StandardOutput.Trim()
                .ShouldBe(version.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1].TrimStart('v'));
            (await client.ExecuteAsync(["help"], cancellationToken)).StandardOutput.ShouldContain("Available Commands");
            foreach (var shell in new[] { "bash", "fish", "powershell", "zsh" })
            {
                (await client.GenerateCompletionAsync(shell, cancellationToken)).ShouldNotBeNullOrWhiteSpace();
            }

            clusterMayExist = true;
            await client.CreateClusterAsync(new KindClusterOptions(Name: clusterName, Wait: TimeSpan.FromMinutes(5)), cancellationToken);

            var clusters = await client.GetClustersAsync(cancellationToken);
            clusters.ShouldContain(clusterName);

            var nodesFromCluster = await client.GetNodesAsync(clusterName, cancellationToken: cancellationToken);
            nodesFromCluster.ShouldContain(node => node.Contains($"{clusterName}-control-plane", StringComparison.Ordinal));
            var nodesFromAllClusters = await client.GetAllNodesAsync(cancellationToken);
            nodesFromAllClusters.ShouldContain(node => node.Contains($"{clusterName}-control-plane", StringComparison.Ordinal));

            var kubeConfig = await client.GetKubeConfigAsync(clusterName, cancellationToken: cancellationToken);
            kubeConfig.ShouldContain($"kind-{clusterName}");
            await client.ExportKubeConfigAsync(clusterName, kubeConfigPath, cancellationToken: cancellationToken);
            File.Exists(kubeConfigPath).ShouldBeTrue();
            (await File.ReadAllTextAsync(kubeConfigPath, cancellationToken)).ShouldContain($"kind-{clusterName}");

            var nodes = await RunKubectlAsync(kubeConfigPath, cancellationToken, "--context", $"kind-{clusterName}", "get", "nodes", "--no-headers");
            var nodeLines = nodes.StandardOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            nodeLines.ShouldNotBeEmpty();
            nodeLines.ShouldContain(line => line.Contains($"{clusterName}-control-plane", StringComparison.Ordinal));
            foreach (var line in nodeLines)
            {
                line.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1].ShouldBe("Ready");
            }

            var imageTag = "busybox:1.37.0";
            await File.WriteAllTextAsync(Path.Combine(imageContextPath, "Dockerfile"), $"FROM {imageTag}\nCMD [\"sleep\", \"3600\"]\n", cancellationToken);
            await RunContainerCliAsync(provider, ["build", "--tag", dockerImage, "--file", Path.Combine(imageContextPath, "Dockerfile"), imageContextPath], cancellationToken);
            await client.LoadDockerImagesAsync(clusterName, [dockerImage], nodesFromCluster, cancellationToken);

            await RunContainerCliAsync(provider, ["tag", dockerImage, archiveImage], cancellationToken);
            await RunContainerCliAsync(provider, ["save", "--output", archivePath, archiveImage], cancellationToken);
            File.Exists(archivePath).ShouldBeTrue();
            await client.LoadImageArchiveAsync(clusterName, archivePath, nodesFromCluster, cancellationToken);

            foreach (var (podName, image) in new[] { ("docker-image-load", dockerImage), ("archive-image-load", archiveImage) })
            {
                await RunKubectlAsync(kubeConfigPath, cancellationToken, "run", podName, "--image", image, "--image-pull-policy=Never", "--restart=Never", "--", "sleep", "3600");
                await RunKubectlAsync(kubeConfigPath, cancellationToken, "wait", "--for=condition=Ready", "--timeout=120s", $"pod/{podName}");
            }

            await client.ExportLogsAsync(clusterName, logsPath, cancellationToken);
            Directory.GetFiles(logsPath, "*", SearchOption.AllDirectories).ShouldNotBeEmpty();

            await client.DeleteClusterAsync(new KindDeleteClusterOptions(clusterName), cancellationToken);
            clusterMayExist = false;

            (await client.GetClustersAsync()).ShouldNotContain(clusterName);
        }
        finally
        {
            if (clusterMayExist)
            {
                await client.DeleteClusterAsync(new KindDeleteClusterOptions(clusterName));
            }

            if (Directory.Exists(workspace)) Directory.Delete(workspace, recursive: true);
        }
    }

    private static async Task<string?> CheckProviderAsync(string provider)
    {
        var arguments = provider.ToLowerInvariant() switch
        {
            "docker" => new[] { "info", "--format", "{{.ServerVersion}}" },
            "podman" => new[] { "info", "--format", "{{.Host.Version}}" },
            "nerdctl" => new[] { "info" },
            _ => throw new InvalidOperationException($"Unsupported KIND_EXPERIMENTAL_PROVIDER '{provider}'.")
        };

        return await CheckCommandAsync(provider, arguments);
    }

    private static async Task<string?> CheckCommandAsync(string executable, IEnumerable<string> arguments)
    {
        var startInfo = new ProcessStartInfo { FileName = executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            var result = await Process.RunAndCaptureTextAsync(startInfo, timeout.Token);
            return result.ExitStatus.ExitCode == 0
                ? null
                : $"'{executable}' exited with code {result.ExitStatus.ExitCode}: {result.StandardError}";
        }
        catch (Exception exception)
        {
            return $"Unable to run '{executable}': {exception.Message}";
        }
    }

    private static async Task<ProcessTextOutput> RunKubectlAsync(string kubeConfigPath, CancellationToken cancellationToken, params string[] arguments)
        => await RunExternalAsync("kubectl", ["--kubeconfig", kubeConfigPath, .. arguments], TimeSpan.FromMinutes(2), cancellationToken);

    private static Task<ProcessTextOutput> RunContainerCliAsync(string provider, IEnumerable<string> arguments, CancellationToken cancellationToken)
        => RunExternalAsync(provider, arguments, TimeSpan.FromMinutes(5), cancellationToken);

    private static async Task<ProcessTextOutput> RunExternalAsync(string executable, IEnumerable<string> arguments, TimeSpan timeoutDuration, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo { FileName = executable, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var timeout = new CancellationTokenSource(timeoutDuration);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var result = await Process.RunAndCaptureTextAsync(startInfo, linked.Token);
        result.ExitStatus.ExitCode.ShouldBe(0, result.StandardError);
        return result;
    }
}
