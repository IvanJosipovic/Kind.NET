using System.Diagnostics;
using System.Text;

namespace Kind.NET;

/// <summary>Provides access to the Kind command-line interface.</summary>
public sealed class KindClient
{
    private static readonly string[] GetClustersArguments = { "get", "clusters" };
    private static readonly string[] VersionArguments = { "version" };
    private static readonly char[] NewlineSeparators = { '\r', '\n' };
    private readonly KindClientOptions _options;
    private readonly string _executable;
    private readonly Func<IReadOnlyList<string>, CancellationToken, Task<KindCommandResult>>? _executor;

    /// <summary>Initializes a client using the bundled or configured executable.</summary>
    public KindClient(KindClientOptions? options = null)
    {
        _options = options ?? new KindClientOptions();
        _executable = KindExecutableResolver.Resolve(_options, KindPlatform.Current);
    }

    internal KindClient(KindClientOptions options, Func<IReadOnlyList<string>, CancellationToken, Task<KindCommandResult>> executor)
        : this(options) => _executor = executor;

    /// <summary>Executes an arbitrary Kind command.</summary>
    public Task<KindCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) => ExecuteCoreAsync(arguments, cancellationToken);
    /// <summary>Creates a cluster.</summary>
    public Task<KindCommandResult> CreateClusterAsync(KindClusterOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.Create(options), cancellationToken);
    /// <summary>Deletes a cluster.</summary>
    public Task<KindCommandResult> DeleteClusterAsync(KindDeleteClusterOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.Delete(options), cancellationToken);
    /// <summary>Lists Kind clusters.</summary>
    public async Task<IReadOnlyList<string>> GetClustersAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(GetClustersArguments, cancellationToken).ConfigureAwait(false)).StandardOutput.Split(NewlineSeparators, StringSplitOptions.RemoveEmptyEntries);
    /// <summary>Gets a cluster kubeconfig as YAML.</summary>
    public async Task<string> GetKubeConfigAsync(string name = "kind", bool internalAddress = false, CancellationToken cancellationToken = default) => (await ExecuteAsync(GetKubeConfigArguments(name, internalAddress), cancellationToken).ConfigureAwait(false)).StandardOutput;
    /// <summary>Exports a cluster kubeconfig.</summary>
    public Task<KindCommandResult> ExportKubeConfigAsync(string name = "kind", string? path = null, bool internalAddress = false, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.ExportKubeConfig(name, path, internalAddress), cancellationToken);
    /// <summary>Exports cluster logs.</summary>
    public Task<KindCommandResult> ExportLogsAsync(string name = "kind", string? path = null, CancellationToken cancellationToken = default) => ExecuteAsync(ExportLogsArguments(name, path), cancellationToken);
    /// <summary>Loads Docker images into a cluster.</summary>
    public Task<KindCommandResult> LoadDockerImagesAsync(string name, IEnumerable<string> images, IEnumerable<string>? nodes = null, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.LoadDockerImages(name, images, nodes), cancellationToken);
    /// <summary>Loads an image archive into a cluster.</summary>
    public Task<KindCommandResult> LoadImageArchiveAsync(string name, string archive, IEnumerable<string>? nodes = null, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.LoadImageArchive(name, archive, nodes), cancellationToken);
    /// <summary>Builds a Kind node image.</summary>
    public Task<KindCommandResult> BuildNodeImageAsync(KindBuildNodeImageOptions options, CancellationToken cancellationToken = default) => ExecuteAsync(KindArgumentBuilder.BuildNodeImage(options), cancellationToken);
    /// <summary>Gets the installed Kind version.</summary>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default) => (await ExecuteAsync(VersionArguments, cancellationToken).ConfigureAwait(false)).StandardOutput;
    /// <summary>Generates a shell completion script.</summary>
    public async Task<string> GenerateCompletionAsync(string shell, CancellationToken cancellationToken = default) => (await ExecuteAsync(new[] { "completion", shell }, cancellationToken).ConfigureAwait(false)).StandardOutput;

    private async Task<KindCommandResult> ExecuteCoreAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        if (_executor is not null) return await _executor(arguments, cancellationToken).ConfigureAwait(false);
        using var process = new Process { StartInfo = new ProcessStartInfo { FileName = _executable, Arguments = string.Join(" ", arguments.Select(Quote)), WorkingDirectory = _options.WorkingDirectory ?? Environment.CurrentDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        if (_options.Environment is not null) foreach (var pair in _options.Environment) process.StartInfo.EnvironmentVariables[pair.Key] = pair.Value;
        try { process.Start(); } catch (Exception ex) { throw new KindCommandException($"Unable to start Kind executable '{_executable}'.", null, null, null, ex); }
        using var timeout = _options.CommandTimeout is null ? null : new CancellationTokenSource(_options.CommandTimeout.Value);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout?.Token ?? CancellationToken.None);
#if NETSTANDARD2_0
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
#else
        var stdout = process.StandardOutput.ReadToEndAsync(linked.Token); var stderr = process.StandardError.ReadToEndAsync(linked.Token);
#endif
        try { await WaitForExitAsync(process, linked.Token).ConfigureAwait(false); }
        catch (OperationCanceledException ex) { try { if (!process.HasExited) process.Kill(); } catch { } throw new KindCommandException("Kind command was cancelled or timed out.", null, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false), ex); }
        var result = new KindCommandResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
        if (result.ExitCode != 0) throw new KindCommandException($"Kind command failed with exit code {result.ExitCode}.", result.ExitCode, result.StandardOutput, result.StandardError);
        return result;
    }

    private static Task<object?> WaitForExitAsync(Process process, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        process.EnableRaisingEvents = true; process.Exited += (_, _) => tcs.TrySetResult(null);
        if (process.HasExited) tcs.TrySetResult(null);
        token.Register(() => tcs.TrySetCanceled(token)); return tcs.Task;
    }

    private static List<string> GetKubeConfigArguments(string name, bool internalAddress)
    {
        var arguments = new List<string> { "get", "kubeconfig", "--name", name };
        if (internalAddress) arguments.Add("--internal");
        return arguments;
    }

    private static List<string> ExportLogsArguments(string name, string? path)
    {
        var arguments = new List<string> { "export", "logs" };
        if (path is not null) arguments.Add(path);
        arguments.Add("--name"); arguments.Add(name);
        return arguments;
    }

    private static string Quote(string value) => value.Length == 0 ? "\"\"" : value.Any(char.IsWhiteSpace) || value.Contains('"') ? "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"" : value;
}
