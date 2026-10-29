using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Kind.NET.Tests;

public sealed class KindClientTests
{
    [Fact]
    public void CreateMapsAllOptionsAndProvider()
    {
        var client = CreateClient("{\"data\":{}}", out var request, new KindClientOptions { Provider = "podman" });
        client.CreateCluster(new KindClusterOptions("demo", "node:test", "config.yaml", "kube.yaml", TimeSpan.FromSeconds(12), true));
        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("create");
        json.GetProperty("name").GetString().ShouldBe("demo");
        json.GetProperty("provider").GetString().ShouldBe("podman");
        json.GetProperty("image").GetString().ShouldBe("node:test");
        json.GetProperty("configPath").GetString().ShouldBe("config.yaml");
        json.GetProperty("kubeConfigPath").GetString().ShouldBe("kube.yaml");
        json.GetProperty("waitMilliseconds").GetInt64().ShouldBe(12000);
        json.GetProperty("retain").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void QueriesReturnTypedDataAndKeepDefaultClusterDistinctFromAllClusters()
    {
        var client = CreateClient("{\"data\":[\"node\"]}", out var request);
        client.GetClusters().ShouldBe(["node"]);
        request().GetProperty("operation").GetString().ShouldBe("clusters");
        client.GetNodes().ShouldBe(["node"]);
        request().GetProperty("name").GetString().ShouldBe("kind");
        client.GetNodes("demo").ShouldBe(["node"]);
        request().GetProperty("name").GetString().ShouldBe("demo");
        client.GetAllNodes().ShouldBe(["node"]);
        request().GetProperty("operation").GetString().ShouldBe("allNodes");
    }

    [Fact]
    public void KubeConfigAndLogOperationsMapPathsAndInternalAddress()
    {
        var text = CreateClient("{\"data\":\"result\"}", out var readText);
        text.GetKubeConfig().ShouldBe("result");
        readText().GetProperty("internalAddress").GetBoolean().ShouldBeFalse();
        text.GetInternalKubeConfig("demo").ShouldBe("result");
        readText().GetProperty("internalAddress").GetBoolean().ShouldBeTrue();
        text.ExportLogs("demo", "logs").ShouldBe("result");
        readText().GetProperty("path").GetString().ShouldBe("logs");
        var client = CreateClient("{\"data\":{}}", out var request);
        client.ExportKubeConfig(path: "external");
        request().GetProperty("name").GetString().ShouldBe("kind");
        request().GetProperty("path").GetString().ShouldBe("external");
        request().GetProperty("internalAddress").GetBoolean().ShouldBeFalse();
        client.ExportInternalKubeConfig("demo", "internal");
        request().GetProperty("internalAddress").GetBoolean().ShouldBeTrue();
        request().GetProperty("path").GetString().ShouldBe("internal");
        client.DeleteCluster(new KindDeleteClusterOptions("demo", "kube"));
        request().GetProperty("operation").GetString().ShouldBe("delete");
        request().GetProperty("kubeConfigPath").GetString().ShouldBe("kube");
    }

    [Fact]
    public void ImageOperationsPreserveImagesNodesAndBuildSettings()
    {
        var client = CreateClient("{\"data\":{}}", out var request);
        client.LoadDockerImages("demo", ["image:a", "image:b"], ["worker"]);
        request().GetProperty("images").GetArrayLength().ShouldBe(2);
        request().GetProperty("nodes")[0].GetString().ShouldBe("worker");
        client.LoadImageArchive("demo", "image.tar");
        request().GetProperty("operation").GetString().ShouldBe("loadImageArchive");
        request().GetProperty("path").GetString().ShouldBe("image.tar");
        client.BuildNodeImage(new KindBuildNodeImageOptions("v1.37.0", "base:test", "node:test", "release", "arm64"));
        var json = request();
        json.GetProperty("operation").GetString().ShouldBe("buildNodeImage");
        json.GetProperty("kubernetesSource").GetString().ShouldBe("v1.37.0");
        json.GetProperty("baseImage").GetString().ShouldBe("base:test");
        json.GetProperty("image").GetString().ShouldBe("node:test");
        json.GetProperty("type").GetString().ShouldBe("release");
        json.GetProperty("architecture").GetString().ShouldBe("arm64");
    }

    [Fact]
    public void InvalidInputsAreRejectedBeforeNativeInvocation()
    {
        var client = new KindClient(new KindClientOptions(), _ => throw new InvalidOperationException("Unexpected invocation"));
        Should.Throw<ArgumentOutOfRangeException>(() => client.CreateCluster(new KindClusterOptions(Wait: TimeSpan.FromSeconds(-1))));
        Should.Throw<ArgumentOutOfRangeException>(() => client.CreateCluster(new KindClusterOptions(Wait: TimeSpan.MaxValue)));
        Should.Throw<ArgumentException>(() => client.LoadDockerImages("demo", []));
        Should.Throw<ArgumentException>(() => client.LoadImageArchive("demo", " "));
    }

    [Fact]
    public void NativeErrorsAndInvalidResponsesAreReported()
    {
        var error = CreateClient("{\"error\":\"cluster failed\"}", out _);
        Should.Throw<KindActionException>(() => error.GetVersion()).Message.ShouldBe("cluster failed");
        foreach (var response in new[] { "{}", "{\"data\":null}" })
        {
            var client = CreateClient(response, out _);
            Should.Throw<KindActionException>(() => client.GetVersion());
        }
        var malformed = CreateClient("invalid", out _);
        Should.Throw<JsonException>(() => malformed.GetVersion());
    }

    [Theory]
    [InlineData("{\"data\":\"0.33.0\"}", false)]
    [InlineData("invalid json", true)]
    [InlineData("{\"error\":\"failed\"}", true)]
    public void NativeResponseIsFreedEvenWhenParsingOrActionFails(string response, bool fails)
    {
        var native = new FakeNativeApi(response);
        var client = new KindClient(new KindClientOptions(), native);
        if (fails) Should.Throw<Exception>(() => client.GetVersion());
        else client.GetVersion().ShouldBe("0.33.0");
        native.Freed.ShouldBeTrue();
        using var request = JsonDocument.Parse(native.Request!);
        request.RootElement.GetProperty("operation").GetString().ShouldBe("version");
    }

    [Fact]
    public void NullNativeResponseIsRejected()
    {
        var native = new FakeNativeApi(null);
        var client = new KindClient(new KindClientOptions(), native);
        Should.Throw<InvalidOperationException>(() => client.GetVersion());
        native.Freed.ShouldBeFalse();
    }

    [Fact]
    public void UndecodableNativeResponseIsFreed()
    {
        var native = new FakeNativeApi("unused") { Undecodable = true };
        var client = new KindClient(new KindClientOptions(), native);
        Should.Throw<InvalidOperationException>(() => client.GetVersion());
        native.Freed.ShouldBeTrue();
    }

    [Fact]
    public void BundledNativeLibraryReturnsActionErrors()
    {
        KindNativeInterop.Invoke(Encoding.UTF8.GetBytes("{\"operation\":\"unsupported\"}"), KindNativeInterop.DefaultApi)
            .ShouldContain("unknown Kind operation");
    }

    private static KindClient CreateClient(string response, out Func<JsonElement> readRequest, KindClientOptions? options = null)
    {
        JsonElement? captured = null;
        var client = new KindClient(options ?? new KindClientOptions(), payload =>
        {
            using var document = JsonDocument.Parse(payload);
            captured = document.RootElement.Clone();
            return response;
        });
        readRequest = () => captured ?? throw new InvalidOperationException("No native request captured.");
        return client;
    }

    private sealed class FakeNativeApi(string? response) : IKindNativeApi
    {
        public string? Request { get; private set; }
        public bool Freed { get; private set; }
        public bool Undecodable { get; init; }
        public nint Execute(nint input)
        {
            Request = Marshal.PtrToStringUTF8(input);
            return response is null ? 0 : Marshal.StringToCoTaskMemUTF8(response);
        }
        public string? GetString(nint value) => Undecodable ? null : Marshal.PtrToStringUTF8(value);
        public void Free(nint value) { Freed = true; Marshal.FreeCoTaskMem(value); }
    }
}
