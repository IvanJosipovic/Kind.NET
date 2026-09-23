using System.Diagnostics;

namespace Kind.NET;

/// <summary>Provides access to the Kind command-line interface.</summary>
public sealed class KindClient
{
    private static readonly string[] s_getClustersArguments = ["get", "clusters"];
    private static readonly string[] s_versionArguments = ["version"];
    private static readonly char[] s_newlineSeparators = ['\r', '\n'];
    private readonly KindClientOptions _options;
    private readonly string _executable;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<KindCommandResult>>? _executor;

    /// <summary>Initializes a client using the bundled or configured executable.</summary>
    /// <param name="options">Executable selection, process settings, and command timeout configuration. When <see langword="null"/>, default options are used.</param>
    public KindClient(KindClientOptions? options = null)
    {
        _options = options ?? new KindClientOptions();
        _executable = !string.IsNullOrWhiteSpace(_options.ExecutablePath)
            ? _options.ExecutablePath!
            : KindExecutableResolver.Resolve(_options, KindPlatform.Current);
    }

    internal KindClient(KindClientOptions options, Func<IReadOnlyList<string>, CancellationToken, Task<KindCommandResult>> executor)
        : this(options) => _executor = executor;

    /// <summary>Executes an arbitrary Kind command.</summary>
    /// <param name="arguments">The arguments passed to the Kind executable, excluding the executable name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) => ExecuteCoreAsync(arguments, cancellationToken);
    /// <summary>Creates a cluster.</summary>
    /// <param name="options">The cluster name and creation settings.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> CreateClusterAsync(KindClusterOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.Create(options), cancellationToken);
    /// <summary>Deletes a cluster.</summary>
    /// <param name="options">The cluster name and deletion settings.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> DeleteClusterAsync(KindDeleteClusterOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.Delete(options), cancellationToken);
    /// <summary>Lists Kind clusters.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<IReadOnlyList<string>> GetClustersAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(s_getClustersArguments, cancellationToken).ConfigureAwait(false)).StandardOutput.Split(s_newlineSeparators, StringSplitOptions.RemoveEmptyEntries);
    /// <summary>Lists the nodes in the default Kind cluster.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<IReadOnlyList<string>> GetNodesAsync(CancellationToken cancellationToken = default) => GetNodesAsync("kind", cancellationToken);
    /// <summary>Lists the nodes in the named Kind cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<IReadOnlyList<string>> GetNodesAsync(string name, CancellationToken cancellationToken = default) => (await ExecuteAsync(GetNodesArguments(name), cancellationToken).ConfigureAwait(false)).StandardOutput.Split(s_newlineSeparators, StringSplitOptions.RemoveEmptyEntries);
    /// <summary>Lists the nodes in every Kind cluster.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<IReadOnlyList<string>> GetAllNodesAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(s_getAllNodesArguments, cancellationToken).ConfigureAwait(false)).StandardOutput.Split(s_newlineSeparators, StringSplitOptions.RemoveEmptyEntries);
    /// <summary>Gets the kubeconfig for the default Kind cluster as YAML.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<string> GetKubeConfigAsync(CancellationToken cancellationToken = default) => GetKubeConfigAsync("kind", cancellationToken);
    /// <summary>Gets the kubeconfig for the named Kind cluster as YAML.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<string> GetKubeConfigAsync(string name, CancellationToken cancellationToken = default) => GetKubeConfigCoreAsync(name, internalAddress: false, cancellationToken);
    /// <summary>Gets an internal-address kubeconfig for the default Kind cluster as YAML.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<string> GetInternalKubeConfigAsync(CancellationToken cancellationToken = default) => GetInternalKubeConfigAsync("kind", cancellationToken);
    /// <summary>Gets an internal-address kubeconfig for the named Kind cluster as YAML.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<string> GetInternalKubeConfigAsync(string name, CancellationToken cancellationToken = default) => GetKubeConfigCoreAsync(name, internalAddress: true, cancellationToken);
    /// <summary>Exports the kubeconfig for the default Kind cluster.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportKubeConfigAsync(CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync("kind", path: null, internalAddress: false, cancellationToken);
    /// <summary>Exports the kubeconfig for the named Kind cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportKubeConfigAsync(string name, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync(name, path: null, internalAddress: false, cancellationToken);
    /// <summary>Exports the kubeconfig for the named Kind cluster to a file.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportKubeConfigAsync(string name, string path, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync(name, path, internalAddress: false, cancellationToken);
    /// <summary>Exports an internal-address kubeconfig for the default Kind cluster.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportInternalKubeConfigAsync(CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync("kind", path: null, internalAddress: true, cancellationToken);
    /// <summary>Exports an internal-address kubeconfig for the named Kind cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportInternalKubeConfigAsync(string name, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync(name, path: null, internalAddress: true, cancellationToken);
    /// <summary>Exports an internal-address kubeconfig for the named Kind cluster to a file.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportInternalKubeConfigAsync(string name, string path, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync(name, path, internalAddress: true, cancellationToken);
    /// <summary>Exports the default cluster kubeconfig to a file.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportDefaultKubeConfigAsync(string path, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync("kind", path, internalAddress: false, cancellationToken);
    /// <summary>Exports an internal-address kubeconfig for the default cluster to a file.</summary>
    /// <param name="path">The destination file path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportDefaultInternalKubeConfigAsync(string path, CancellationToken cancellationToken = default) => ExportKubeConfigCoreAsync("kind", path, internalAddress: true, cancellationToken);
    /// <summary>Exports logs for the default Kind cluster.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportLogsAsync(CancellationToken cancellationToken = default) => ExportLogsCoreAsync("kind", path: null, cancellationToken);
    /// <summary>Exports logs for the named Kind cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportLogsAsync(string name, CancellationToken cancellationToken = default) => ExportLogsCoreAsync(name, path: null, cancellationToken);
    /// <summary>Exports logs for the named Kind cluster to a directory.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="path">The destination directory path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportLogsAsync(string name, string path, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.ExportLogs(name, path), cancellationToken);
    /// <summary>Exports logs for the default Kind cluster to a directory.</summary>
    /// <param name="path">The destination directory path.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> ExportDefaultLogsAsync(string path, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.ExportLogs("kind", path), cancellationToken);
    /// <summary>Loads Docker images into a cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="images">The Docker image names to load.</param>
    /// <param name="nodes">The node names to load the images into, or <see langword="null"/> to load them into all nodes.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> LoadDockerImagesAsync(string name, IEnumerable<string> images, IEnumerable<string>? nodes = null, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.LoadDockerImages(name, images, nodes), cancellationToken);
    /// <summary>Loads an image archive into a cluster.</summary>
    /// <param name="name">The Kind cluster name.</param>
    /// <param name="archive">The path to the image archive.</param>
    /// <param name="nodes">The node names to load the archive into, or <see langword="null"/> to load it into all nodes.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> LoadImageArchiveAsync(string name, string archive, IEnumerable<string>? nodes = null, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.LoadImageArchive(name, archive, nodes), cancellationToken);
    /// <summary>Builds a Kind node image.</summary>
    /// <param name="options">The Kubernetes source and node-image build settings.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public Task<KindCommandResult> BuildNodeImageAsync(KindBuildNodeImageOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.BuildNodeImage(options), cancellationToken);
    /// <summary>Gets the installed Kind version.</summary>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(s_versionArguments, cancellationToken).ConfigureAwait(false)).StandardOutput;
    /// <summary>Generates a shell completion script.</summary>
    /// <param name="shell">The shell for which to generate completion, such as <c>bash</c>, <c>fish</c>, <c>powershell</c>, or <c>zsh</c>.</param>
    /// <param name="cancellationToken">A token that cancels the command.</param>
    public async Task<string> GenerateCompletionAsync(string shell, CancellationToken cancellationToken = default) => (await ExecuteAsync(new[] { "completion", shell }, cancellationToken).ConfigureAwait(false)).StandardOutput;

    private async Task<KindCommandResult> ExecuteCoreAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (_executor is not null) return await _executor(arguments, cancellationToken).ConfigureAwait(false);
        var startInfo = new ProcessStartInfo
        {
            FileName = _executable,
            WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        startInfo.InheritedHandles = [];
        if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux()) startInfo.KillOnParentExit = true;
        if (_options.Environment is not null) foreach (var pair in _options.Environment) startInfo.EnvironmentVariables[pair.Key] = pair.Value;

        using var timeout = _options.CommandTimeout is null ? null : new CancellationTokenSource(_options.CommandTimeout.Value);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout?.Token ?? CancellationToken.None);

        ProcessTextOutput output;
        try
        {
            output = await Process.RunAndCaptureTextAsync(startInfo, linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            throw new KindCommandException("Kind command was cancelled or timed out.", null, null, null, ex);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
        {
            throw new KindCommandException($"Unable to start Kind executable '{_executable}'.", null, null, null, ex);
        }

        var result = new KindCommandResult(output.ExitStatus.ExitCode, output.StandardOutput, output.StandardError);
        if (result.ExitCode != 0) throw new KindCommandException($"Kind command failed with exit code {result.ExitCode}.", result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }

    private static List<string> GetKubeConfigArguments(string name, bool internalAddress)
    {
        var arguments = new List<string> { "get", "kubeconfig", "--name", name };
        if (internalAddress) arguments.Add("--internal");
        return arguments;
    }

    private async Task<string> GetKubeConfigCoreAsync(string name, bool internalAddress, CancellationToken cancellationToken) => (await ExecuteAsync(GetKubeConfigArguments(name, internalAddress), cancellationToken).ConfigureAwait(false)).StandardOutput;

    private Task<KindCommandResult> ExportKubeConfigCoreAsync(string name, string? path, bool internalAddress, CancellationToken cancellationToken) => ExecuteAsync(KindArgumentBuilder.ExportKubeConfig(name, path, internalAddress), cancellationToken);

    private Task<KindCommandResult> ExportLogsCoreAsync(string name, string? path, CancellationToken cancellationToken) => ExecuteAsync(KindArgumentBuilder.ExportLogs(name, path), cancellationToken);

    private static readonly string[] s_getAllNodesArguments = ["get", "nodes", "--all-clusters"];

    private static List<string> GetNodesArguments(string name)
    {
        return ["get", "nodes", "--name", name];
    }

}
