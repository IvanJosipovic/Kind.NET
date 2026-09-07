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
public sealed record KindClusterOptions(
    string Name = "kind",
    string? Image = null,
    string? ConfigPath = null,
    string? KubeConfigPath = null,
    TimeSpan? Wait = null,
    bool Retain = false);

/// <summary>Options for deleting a Kind cluster.</summary>
public sealed record KindDeleteClusterOptions(string Name = "kind", string? KubeConfigPath = null);

/// <summary>Options for building a Kind node image.</summary>
public sealed record KindBuildNodeImageOptions(
    string? KubernetesSource = null,
    string? BaseImage = null,
    string? Image = null,
    string? Type = null);

/// <summary>The result of a Kind command.</summary>
public sealed record KindCommandResult(int ExitCode, string StandardOutput, string StandardError);

/// <summary>Represents a failed Kind command.</summary>
public sealed class KindCommandException : Exception
{
    /// <summary>Initializes a command exception with captured process details.</summary>
    public KindCommandException(string message, int? exitCode = null, string? standardOutput = null, string? standardError = null, Exception? innerException = null)
        : base(message, innerException) => (ExitCode, StandardOutput, StandardError) = (exitCode, standardOutput, standardError);

    /// <summary>Gets the process exit code.</summary>
    public int? ExitCode { get; }
    /// <summary>Gets captured standard output.</summary>
    public string? StandardOutput { get; }
    /// <summary>Gets captured standard error.</summary>
    public string? StandardError { get; }
}
