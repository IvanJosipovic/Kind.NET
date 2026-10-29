namespace Kind.NET;

/// <summary>Typed synchronous Kind operations. Container tools are still required for cluster operations.</summary>
public interface IKindClient
{
    /// <summary>Creates a cluster using the supplied configuration.</summary>
    void CreateCluster(KindClusterOptions options);
    /// <summary>Deletes a cluster and updates its kubeconfig.</summary>
    void DeleteCluster(KindDeleteClusterOptions options);
    /// <summary>Lists all Kind cluster names.</summary>
    IReadOnlyList<string> GetClusters();
    /// <summary>Lists nodes in the named cluster; defaults to kind.</summary>
    IReadOnlyList<string> GetNodes(string name = "kind");
    /// <summary>Lists nodes across all Kind clusters.</summary>
    IReadOnlyList<string> GetAllNodes();
    /// <summary>Returns the external-address kubeconfig YAML.</summary>
    string GetKubeConfig(string name = "kind");
    /// <summary>Returns the internal-address kubeconfig YAML.</summary>
    string GetInternalKubeConfig(string name = "kind");
    /// <summary>Exports external-address kubeconfig to a file, or Kind's default location.</summary>
    void ExportKubeConfig(string name = "kind", string? path = null);
    /// <summary>Exports internal-address kubeconfig to a file, or Kind's default location.</summary>
    void ExportInternalKubeConfig(string name = "kind", string? path = null);
    /// <summary>Collects cluster logs and returns their directory; creates a temporary directory when path is omitted.</summary>
    string ExportLogs(string name = "kind", string? path = null);
    /// <summary>Loads images from the local Docker image store into selected nodes, or all nodes.</summary>
    void LoadDockerImages(string name, IEnumerable<string> images, IEnumerable<string>? nodes = null);
    /// <summary>Loads an image archive into selected nodes, or all nodes.</summary>
    void LoadImageArchive(string name, string archive, IEnumerable<string>? nodes = null);
    /// <summary>Builds a Kind node image.</summary>
    void BuildNodeImage(KindBuildNodeImageOptions options);
    /// <summary>Returns the bundled Kind semantic version.</summary>
    string GetVersion();
}
