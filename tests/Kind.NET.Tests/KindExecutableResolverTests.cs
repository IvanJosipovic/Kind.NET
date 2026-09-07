namespace Kind.NET.Tests;

public sealed class KindExecutableResolverTests
{
    [Fact]
    public void ExplicitPathTakesPrecedence()
    {
        var result = KindExecutableResolver.Resolve(new KindClientOptions { ExecutablePath = "custom-kind" }, new KindPlatform("linux-x64", "kind"), "does-not-exist");
        result.ShouldBe("custom-kind");
    }

    [Fact]
    public void BundledAssetIsResolvedFromRidDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "runtimes", "linux-x64", "native", "kind");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        try
        {
            KindExecutableResolver.Resolve(new KindClientOptions(), new KindPlatform("linux-x64", "kind"), root).ShouldBe(path);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DisabledBundlingFallsBackToPathLookup()
    {
        KindExecutableResolver.Resolve(new KindClientOptions { UseBundledExecutable = false }, new KindPlatform("linux-x64", "kind"), "missing").ShouldBe("kind");
    }

    [Fact]
    public void RootBundledAssetIsResolved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "kind");
        File.WriteAllText(path, "test");
        try { KindExecutableResolver.Resolve(new KindClientOptions(), new KindPlatform("linux-x64", "kind"), root).ShouldBe(path); }
        finally { Directory.Delete(root, true); }
    }
}
