using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Kind.NET;

/// <summary>Provides typed access to Kind's Go library through the bundled native shim.</summary>
public sealed class KindClient : IKindClient
{
    private readonly KindClientOptions _options;
    private readonly Func<byte[], string>? _nativeCall;
    private readonly IKindNativeApi _nativeApi;

    /// <summary>Initializes a native Kind client.</summary>
    public KindClient(KindClientOptions? options = null)
        : this(options ?? new KindClientOptions(), KindNativeInterop.DefaultApi) { }

    internal KindClient(KindClientOptions options, IKindNativeApi nativeApi) =>
        (_options, _nativeApi) = (options, nativeApi);

    internal KindClient(KindClientOptions options, Func<byte[], string> nativeCall)
        : this(options, KindNativeInterop.DefaultApi) => _nativeCall = nativeCall;

    /// <inheritdoc />
    public void CreateCluster(KindClusterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Wait is { } wait && (wait < TimeSpan.Zero || wait.TotalMilliseconds > long.MaxValue / 1_000_000))
            throw new ArgumentOutOfRangeException(nameof(options), "Wait must be a nonnegative Go duration.");
        _ = Invoke(new NativeKindRequest("create", options.Name, Image: options.Image,
            ConfigPath: options.ConfigPath, KubeConfigPath: options.KubeConfigPath,
            WaitMilliseconds: (long)(options.Wait?.TotalMilliseconds ?? 0), Retain: options.Retain), NativeJsonContext.Default.NativeKindAck);
    }

    /// <inheritdoc />
    public void DeleteCluster(KindDeleteClusterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = Invoke(new NativeKindRequest("delete", options.Name, KubeConfigPath: options.KubeConfigPath), NativeJsonContext.Default.NativeKindAck);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetClusters() => Invoke(new NativeKindRequest("clusters"), NativeJsonContext.Default.StringArray);
    /// <inheritdoc />
    public IReadOnlyList<string> GetNodes(string name = "kind") => Invoke(new NativeKindRequest("nodes", name), NativeJsonContext.Default.StringArray);
    /// <inheritdoc />
    public IReadOnlyList<string> GetAllNodes() => Invoke(new NativeKindRequest("allNodes"), NativeJsonContext.Default.StringArray);
    /// <inheritdoc />
    public string GetKubeConfig(string name = "kind") => Invoke(new NativeKindRequest("kubeConfig", name), NativeJsonContext.Default.String);
    /// <inheritdoc />
    public string GetInternalKubeConfig(string name = "kind") => Invoke(new NativeKindRequest("kubeConfig", name, InternalAddress: true), NativeJsonContext.Default.String);
    /// <inheritdoc />
    public void ExportKubeConfig(string name = "kind", string? path = null) =>
        _ = Invoke(new NativeKindRequest("exportKubeConfig", name, Path: path), NativeJsonContext.Default.NativeKindAck);
    /// <inheritdoc />
    public void ExportInternalKubeConfig(string name = "kind", string? path = null) =>
        _ = Invoke(new NativeKindRequest("exportKubeConfig", name, Path: path, InternalAddress: true), NativeJsonContext.Default.NativeKindAck);
    /// <inheritdoc />
    public string ExportLogs(string name = "kind", string? path = null) => Invoke(new NativeKindRequest("exportLogs", name, Path: path), NativeJsonContext.Default.String);
    /// <inheritdoc />
    public void LoadDockerImages(string name, IEnumerable<string> images, IEnumerable<string>? nodes = null)
    {
        ArgumentNullException.ThrowIfNull(images);
        var imageArray = images.ToArray();
        if (imageArray.Length == 0 || imageArray.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one nonempty image name is required.", nameof(images));
        _ = Invoke(new NativeKindRequest("loadDockerImages", name, Images: imageArray, Nodes: nodes?.ToArray()), NativeJsonContext.Default.NativeKindAck);
    }
    /// <inheritdoc />
    public void LoadImageArchive(string name, string archive, IEnumerable<string>? nodes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(archive);
        _ = Invoke(new NativeKindRequest("loadImageArchive", name, Path: archive, Nodes: nodes?.ToArray()), NativeJsonContext.Default.NativeKindAck);
    }
    /// <inheritdoc />
    public void BuildNodeImage(KindBuildNodeImageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = Invoke(new NativeKindRequest("buildNodeImage", Image: options.Image, KubernetesSource: options.KubernetesSource,
            BaseImage: options.BaseImage, Type: options.Type, Architecture: options.Architecture), NativeJsonContext.Default.NativeKindAck);
    }
    /// <inheritdoc />
    public string GetVersion() => Invoke(new NativeKindRequest("version"), NativeJsonContext.Default.String);

    private T Invoke<T>(NativeKindRequest request, JsonTypeInfo<T> resultType)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(request with { Provider = _options.Provider }, NativeJsonContext.Default.NativeKindRequest);
        var json = _nativeCall is null ? KindNativeInterop.Invoke(payload, _nativeApi) : _nativeCall(payload);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String)
            throw new KindActionException(error.GetString() ?? "Kind action failed.");
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            throw new KindActionException("The Kind native library returned no action data.");
        return data.Deserialize(resultType) ?? throw new KindActionException("The Kind native library returned invalid action data.");
    }
}
