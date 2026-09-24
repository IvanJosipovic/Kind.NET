namespace Kind.NET;

/// <summary>Configures a <see cref="KindClient"/>.</summary>
public sealed class KindClientOptions
{
    /// <summary>Gets the explicit Kind executable path.</summary>
    public string? ExecutablePath { get; init; }
    /// <summary>Gets whether the bundled executable is preferred.</summary>
    public bool UseBundledExecutable { get; init; } = true;
    /// <summary>Gets the command working directory.</summary>
    public string? WorkingDirectory { get; init; }
    /// <summary>Gets the command timeout.</summary>
    public TimeSpan? CommandTimeout { get; init; }
    /// <summary>Gets environment variables added to Kind processes.</summary>
    public IReadOnlyDictionary<string, string?>? Environment { get; init; }
}

/// <summary>Options for creating a Kind cluster.</summary>
/// <param name="Name">The cluster name. Defaults to <c>kind</c>.</param>
/// <param name="Image">The node image to use, or <see langword="null"/> to use Kind's default image.</param>
/// <param name="ConfigPath">The Kind configuration file path, or <see langword="null"/> to create a default cluster.</param>
/// <param name="KubeConfigPath">The kubeconfig file path to write, or <see langword="null"/> to use Kind's default path.</param>
/// <param name="Wait">The maximum time to wait for the control plane, or <see langword="null"/> to use Kind's default.</param>
/// <param name="Retain">Whether to retain the node containers if cluster creation fails.</param>
public sealed record KindClusterOptions(
    string Name = "kind",
    string? Image = null,
    string? ConfigPath = null,
    string? KubeConfigPath = null,
    TimeSpan? Wait = null,
    bool Retain = false);

/// <summary>Options for deleting a Kind cluster.</summary>
/// <param name="Name">The cluster name. Defaults to <c>kind</c>.</param>
/// <param name="KubeConfigPath">The kubeconfig file path, or <see langword="null"/> to use Kind's default path.</param>
public sealed record KindDeleteClusterOptions(string Name = "kind", string? KubeConfigPath = null);

/// <summary>Options for building a Kind node image.</summary>
/// <param name="KubernetesSource">The Kubernetes source directory or version to build from.</param>
/// <param name="BaseImage">The base image used for the node image build.</param>
/// <param name="Image">The output node image name.</param>
/// <param name="Type">The Kubernetes source type, such as <c>release</c> or <c>source</c>.</param>
/// <param name="Architecture">The target architecture, such as <c>amd64</c> or <c>arm64</c>.</param>
public sealed record KindBuildNodeImageOptions(
    string? KubernetesSource = null,
    string? BaseImage = null,
    string? Image = null,
    string? Type = null,
    string? Architecture = null);

/// <summary>The result of a Kind command.</summary>
/// <param name="ExitCode">The process exit code.</param>
/// <param name="StandardOutput">The captured standard output.</param>
/// <param name="StandardError">The captured standard error.</param>
public sealed record KindCommandResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Represents a failed Kind command.</summary>
public sealed class KindCommandException : Exception
{
    /// <summary>Initializes a command exception with captured process details.</summary>
    /// <param name="message">The exception message.</param>
    /// <param name="exitCode">The Kind process exit code, or <see langword="null"/> if the process did not exit normally.</param>
    /// <param name="standardOutput">The captured standard output, if available.</param>
    /// <param name="standardError">The captured standard error, if available.</param>
    /// <param name="innerException">The exception that caused this exception, if any.</param>
    public KindCommandException(string message, int? exitCode = null, string? standardOutput = null, string? standardError = null, Exception? innerException = null)
        : base(message, innerException) => (ExitCode, StandardOutput, StandardError) = (exitCode, standardOutput, standardError);

    /// <summary>Gets the process exit code.</summary>
    public int? ExitCode { get; }
    /// <summary>Gets captured standard output.</summary>
    public string? StandardOutput { get; }
    /// <summary>Gets captured standard error.</summary>
    public string? StandardError { get; }
}
