namespace Kind.NET;

/// <summary>Configures the native Kind client.</summary>
public sealed class KindClientOptions
{
    /// <summary>Gets the container provider: docker, podman, or nerdctl. When omitted, Kind selects the provider.</summary>
    public string? Provider { get; init; }
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

/// <summary>Represents a failed native Kind action.</summary>
public sealed class KindActionException : Exception
{
    /// <summary>Initializes an exception with the native action error.</summary>
    public KindActionException(string message) : base(message) { }
}
