namespace Kind.NET.Tests;

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

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
        var architecture = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "amd64",
            Architecture.Arm64 => "arm64",
            _ => throw new PlatformNotSupportedException($"Unsupported test architecture {RuntimeInformation.ProcessArchitecture}.")
        };
        var client = new KindClient(new KindClientOptions { CommandTimeout = TimeSpan.FromMinutes(12) });
        try
        {
            await client.BuildNodeImageAsync(new KindBuildNodeImageOptions(
                "v1.37.0",
                BaseImage: "docker.io/kindest/base:v20260820-69b56db7",
                Image: image,
                Type: "release",
                Architecture: architecture), cancellationToken);
            (await RunContainerCliAsync(provider, ["image", "inspect", image], cancellationToken)).StandardOutput.ShouldContain(image);
        }
        finally
        {
            try
            {
                await RunContainerCliAsync(provider, ["image", "rm", "--force", image], CancellationToken.None);
            }
            catch
            {
                // Cleanup must not hide the failure that caused this finally block.
            }
        }
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
        var secondClusterName = $"kn-{Guid.NewGuid():N}";
        var workspace = Path.Combine(Path.GetTempPath(), $"kind-net-{clusterName}");
        Directory.CreateDirectory(workspace);
        var configPath = Path.Combine(workspace, "kind-config.yaml");
        var kubeConfigPath = Path.Combine(workspace, "kubeconfig.yaml");
        var exportedKubeConfigPath = Path.Combine(workspace, "exported-kubeconfig.yaml");
        var exportedInternalKubeConfigPath = Path.Combine(workspace, "exported-internal-kubeconfig.yaml");
        var logsPath = Path.Combine(workspace, "logs");
        var archivePath = Path.Combine(workspace, "image.tar");
        var imageContextPath = Path.Combine(workspace, "image");
        Directory.CreateDirectory(imageContextPath);
        var dockerImage = $"kind-net-test:{Guid.NewGuid():N}";
        var archiveImage = $"{dockerImage}-archive";
        var client = new KindClient(new KindClientOptions
        {
            CommandTimeout = TimeSpan.FromMinutes(12),
            Environment = new Dictionary<string, string?> { ["KUBECONFIG"] = kubeConfigPath }
        });
        var clusterMayExist = false;
        var secondClusterMayExist = false;

        try
        {
            await File.WriteAllTextAsync(configPath, "kind: Cluster\napiVersion: kind.x-k8s.io/v1alpha4\nnodes:\n- role: control-plane\n- role: worker\n", cancellationToken);
            var version = await client.GetVersionAsync(cancellationToken);
            version.ShouldContain("kind v");
            (await client.ExecuteAsync(["--quiet", "version"], cancellationToken)).StandardOutput.Trim()
                .ShouldBe(version.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1].TrimStart('v'));
            (await client.ExecuteAsync(["help"], cancellationToken)).StandardOutput.ShouldContain("Available Commands");
            (await client.ExecuteAsync(["--verbosity", "0", "version"], cancellationToken)).StandardOutput.ShouldContain("kind v");
            foreach (var shell in new[] { "bash", "fish", "powershell", "zsh" })
            {
                (await client.GenerateCompletionAsync(shell, cancellationToken)).ShouldNotBeNullOrWhiteSpace();
            }

            clusterMayExist = true;
            await client.CreateClusterAsync(new KindClusterOptions(
                Name: clusterName,
                Image: "kindest/node:v1.37.0",
                ConfigPath: configPath,
                KubeConfigPath: kubeConfigPath,
                Wait: TimeSpan.FromMinutes(5),
                Retain: true), cancellationToken);
            File.Exists(kubeConfigPath).ShouldBeTrue();

            secondClusterMayExist = true;
            await client.CreateClusterAsync(new KindClusterOptions(
                Name: secondClusterName,
                Image: "kindest/node:v1.37.0",
                Wait: TimeSpan.FromMinutes(5)), cancellationToken);

            var clusters = await client.GetClustersAsync(cancellationToken);
            clusters.ShouldContain(clusterName);
            clusters.ShouldContain(secondClusterName);

            var nodesFromCluster = await client.GetNodesAsync(clusterName, cancellationToken: cancellationToken);
            nodesFromCluster.ShouldContain(node => node.Contains($"{clusterName}-control-plane", StringComparison.Ordinal));
            var workerNode = $"{clusterName}-worker";
            var controlPlaneNode = $"{clusterName}-control-plane";
            nodesFromCluster.ShouldContain(workerNode);
            (await client.GetNodesAsync(secondClusterName, cancellationToken)).ShouldContain($"{secondClusterName}-control-plane");
            var nodesFromAllClusters = await client.GetAllNodesAsync(cancellationToken);
            nodesFromAllClusters.ShouldContain(controlPlaneNode);
            nodesFromAllClusters.ShouldContain(workerNode);
            nodesFromAllClusters.ShouldContain($"{secondClusterName}-control-plane");

            var kubeConfig = await client.GetKubeConfigAsync(clusterName, cancellationToken: cancellationToken);
            kubeConfig.ShouldContain($"kind-{clusterName}");
            var internalKubeConfig = await client.GetInternalKubeConfigAsync(clusterName, cancellationToken);
            internalKubeConfig.ShouldContain($"kind-{clusterName}");
            await client.ExportKubeConfigAsync(clusterName, exportedKubeConfigPath, cancellationToken);
            await client.ExportInternalKubeConfigAsync(clusterName, exportedInternalKubeConfigPath, cancellationToken);
            File.Exists(exportedKubeConfigPath).ShouldBeTrue();
            File.Exists(exportedInternalKubeConfigPath).ShouldBeTrue();
            (await File.ReadAllTextAsync(exportedKubeConfigPath, cancellationToken)).ShouldContain($"kind-{clusterName}");
            (await File.ReadAllTextAsync(exportedInternalKubeConfigPath, cancellationToken)).ShouldContain($"kind-{clusterName}");

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
            await RunContainerCliAsync(provider, ["tag", dockerImage, archiveImage], cancellationToken);
            await client.LoadDockerImagesAsync(clusterName, [dockerImage, archiveImage], [workerNode], cancellationToken);
            await RunContainerCliAsync(provider, ["save", "--output", archivePath, archiveImage], cancellationToken);
            File.Exists(archivePath).ShouldBeTrue();
            await client.LoadImageArchiveAsync(clusterName, archivePath, cancellationToken: cancellationToken);

            foreach (var (podName, image, nodeName) in new[]
            {
                ("docker-image-load", dockerImage, workerNode),
                ("archive-image-load", archiveImage, controlPlaneNode)
            })
            {
                var overrides = JsonSerializer.Serialize(new { spec = new { nodeName } });
                await RunKubectlAsync(kubeConfigPath, cancellationToken, "--context", $"kind-{clusterName}", "run", podName, "--image", image, "--image-pull-policy=Never", "--restart=Never", "--overrides", overrides, "--", "sleep", "3600");
                await RunKubectlAsync(kubeConfigPath, cancellationToken, "--context", $"kind-{clusterName}", "wait", "--for=condition=Ready", "--timeout=120s", $"pod/{podName}");
            }

            await client.ExportLogsAsync(clusterName, logsPath, cancellationToken);
            Directory.GetFiles(logsPath, "*", SearchOption.AllDirectories).ShouldNotBeEmpty();

            await client.DeleteClusterAsync(new KindDeleteClusterOptions(secondClusterName, kubeConfigPath), cancellationToken);
            secondClusterMayExist = false;
            (await client.GetClustersAsync(cancellationToken)).ShouldNotContain(secondClusterName);

            await client.DeleteClusterAsync(new KindDeleteClusterOptions(clusterName, kubeConfigPath), cancellationToken);
            clusterMayExist = false;

            (await client.GetClustersAsync()).ShouldNotContain(clusterName);
        }
        finally
        {
            if (clusterMayExist)
            {
                await client.DeleteClusterAsync(new KindDeleteClusterOptions(clusterName, kubeConfigPath));
            }

            if (secondClusterMayExist)
            {
                await client.DeleteClusterAsync(new KindDeleteClusterOptions(secondClusterName, kubeConfigPath));
            }

            try
            {
                await RunContainerCliAsync(provider, ["image", "rm", "--force", dockerImage, archiveImage], CancellationToken.None);
            }
            catch
            {
                // Cleanup must not hide the failure that caused this finally block.
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
