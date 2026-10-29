using System.Diagnostics;

namespace Kind.NET.Scenarios;

/// <summary>Exercises the same public API through managed and NativeAOT hosts.</summary>
public static class KindClientScenarios
{
    /// <summary>Runs native-loading checks and optional container-backed lifecycle scenarios.</summary>
    public static void RunAll(IKindClient kind, bool clusterLifecycle)
    {
        ArgumentNullException.ThrowIfNull(kind);
        Require(Version.TryParse(kind.GetVersion().Split('-')[0], out _), "Kind did not return a semantic version.");
        var invalid = new KindClient(new KindClientOptions { Provider = "invalid-provider" });
        try
        {
            invalid.GetClusters();
            throw new InvalidOperationException("Invalid provider unexpectedly succeeded.");
        }
        catch (KindActionException exception)
        {
            Require(exception.Message.Contains("unsupported container provider", StringComparison.Ordinal), "Native error did not cross the boundary.");
        }
        if (!clusterLifecycle) return;

        var name = $"kind-net-{Guid.NewGuid():N}";
        var second = $"kind-net-{Guid.NewGuid():N}";
        var root = Path.Combine(Path.GetTempPath(), name);
        Directory.CreateDirectory(root);
        var kubeconfig = Path.Combine(root, "kubeconfig");
        var config = Path.Combine(root, "kind.yaml");
        var image = $"kind-net-test:{Guid.NewGuid():N}";
        var firstMayExist = false;
        var secondMayExist = false;
        var cleanupFailures = new List<Exception>();
        try
        {
            File.WriteAllText(config, "kind: Cluster\napiVersion: kind.x-k8s.io/v1alpha4\nnodes:\n- role: control-plane\n- role: worker\n");
            firstMayExist = true;
            kind.CreateCluster(new KindClusterOptions(name, ConfigPath: config, KubeConfigPath: kubeconfig, Wait: TimeSpan.FromMinutes(3)));
            Require(kind.GetClusters().Contains(name), "Created cluster was not listed.");
            var nodes = kind.GetNodes(name);
            Require(nodes.Count == 2, "Expected control-plane and worker nodes.");
            Require(nodes.All(kind.GetAllNodes().Contains), "All-nodes query omitted a node.");
            Require(kind.GetKubeConfig(name).Contains("server:", StringComparison.Ordinal), "External kubeconfig has no server.");
            Require(kind.GetInternalKubeConfig(name).Contains(name + "-control-plane", StringComparison.Ordinal), "Internal kubeconfig has no internal address.");
            var exported = Path.Combine(root, "exported");
            var internalExported = Path.Combine(root, "internal");
            kind.ExportKubeConfig(name, exported);
            kind.ExportInternalKubeConfig(name, internalExported);
            Require(File.ReadAllText(exported).Contains("server:", StringComparison.Ordinal), "Kubeconfig export failed.");
            Require(File.ReadAllText(internalExported).Contains(name + "-control-plane", StringComparison.Ordinal), "Internal export failed.");
            Run("kubectl", "--kubeconfig", kubeconfig, "wait", "--for=condition=Ready", "nodes", "--all", "--timeout=180s");
            var logs = kind.ExportLogs(name, Path.Combine(root, "logs"));
            Require(Directory.EnumerateFiles(logs, "*", SearchOption.AllDirectories).Any(), "No logs were exported.");

            // Docker images can be imported into any selected Kind provider.
            var context = Path.Combine(root, "image");
            Directory.CreateDirectory(context);
            File.WriteAllText(Path.Combine(context, "Dockerfile"), "FROM busybox:1.37\nLABEL kind-net-test=true\n");
            Run("docker", "build", "-t", image, context);
            kind.LoadDockerImages(name, [image], [nodes[0]]);
            var archive = Path.Combine(root, "image.tar");
            Run("docker", "save", "-o", archive, image);
            kind.LoadImageArchive(name, archive, [nodes[1]]);
            foreach (var node in nodes)
                Run(Environment.GetEnvironmentVariable("KIND_EXPERIMENTAL_PROVIDER") ?? "docker", "exec", node, "crictl", "inspecti", image);
            try
            {
                kind.LoadImageArchive(name, archive, ["missing-node"]);
                throw new InvalidOperationException("Unknown node was accepted.");
            }
            catch (KindActionException) { }

            secondMayExist = true;
            kind.CreateCluster(new KindClusterOptions(second, KubeConfigPath: kubeconfig, Wait: TimeSpan.FromMinutes(3)));
            Require(kind.GetClusters().Contains(second), "Second cluster was not listed.");
            Require(kind.GetAllNodes().Contains(second + "-control-plane"), "All-nodes query omitted second cluster.");
            kind.DeleteCluster(new KindDeleteClusterOptions(second, kubeconfig));
            secondMayExist = false;
            kind.DeleteCluster(new KindDeleteClusterOptions(name, kubeconfig));
            firstMayExist = false;
            Require(!kind.GetClusters().Contains(name) && !kind.GetClusters().Contains(second), "Deleted cluster was still listed.");

            if (string.Equals(Environment.GetEnvironmentVariable("KIND_TEST_BUILD_NODE_IMAGE"), "true", StringComparison.OrdinalIgnoreCase))
            {
                kind.BuildNodeImage(new KindBuildNodeImageOptions("v1.37.0", Image: image, Type: "release"));
                Run("docker", "image", "inspect", image);
            }
        }
        finally
        {
            void Cleanup(Action action)
            {
                try { action(); }
                catch (Exception exception)
                {
                    cleanupFailures.Add(exception);
                    Console.Error.WriteLine($"Scenario cleanup failed: {exception.Message}");
                }
            }
            if (secondMayExist) Cleanup(() => kind.DeleteCluster(new KindDeleteClusterOptions(second, kubeconfig)));
            if (firstMayExist) Cleanup(() => kind.DeleteCluster(new KindDeleteClusterOptions(name, kubeconfig)));
            // The image may never have been created if cluster setup failed.
            try { Run("docker", "image", "rm", "--force", image); }
            catch (InvalidOperationException) { }
            Cleanup(() => Directory.Delete(root, recursive: true));
        }
        if (cleanupFailures.Count > 0)
            throw new AggregateException("Scenario cleanup failed.", cleanupFailures);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException($"Could not start {executable}.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(600_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException($"{executable} timed out.");
        }
        Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
        Require(process.ExitCode == 0, $"{executable} failed: {stderr.Result} {stdout.Result}");
    }
}
