using System.Text.Json.Serialization;

namespace Kind.NET;

internal sealed record NativeKindRequest(
    string Operation,
    string Name = "kind",
    string? Provider = null,
    string? Image = null,
    string? ConfigPath = null,
    string? KubeConfigPath = null,
    long WaitMilliseconds = 0,
    bool Retain = false,
    bool InternalAddress = false,
    string? Path = null,
    string[]? Images = null,
    string[]? Nodes = null,
    string? KubernetesSource = null,
    string? BaseImage = null,
    string? Type = null,
    string? Architecture = null);

internal sealed class NativeKindAck { }

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(NativeKindRequest))]
[JsonSerializable(typeof(NativeKindAck))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(string))]
internal partial class NativeJsonContext : JsonSerializerContext;
