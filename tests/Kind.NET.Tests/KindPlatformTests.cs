using System.Runtime.InteropServices;

namespace Kind.NET.Tests;

public sealed class KindPlatformTests
{
    [Theory]
    [InlineData("Windows", Architecture.X64, "win-x64", "kind.exe")]
    [InlineData("Windows", Architecture.Arm64, "win-arm64", "kind.exe")]
    [InlineData("Linux", Architecture.X64, "linux-x64", "kind")]
    [InlineData("Linux", Architecture.Arm64, "linux-arm64", "kind")]
    [InlineData("Darwin", Architecture.X64, "osx-x64", "kind")]
    [InlineData("Darwin", Architecture.Arm64, "osx-arm64", "kind")]
    public void FromMapsSupportedPlatforms(string os, Architecture architecture, string rid, string fileName)
    {
        var platform = KindPlatform.From(os, architecture);
        platform.Rid.ShouldBe(rid);
        platform.FileName.ShouldBe(fileName);
    }

    [Fact]
    public void FromRejectsUnsupportedArchitecture() => Should.Throw<PlatformNotSupportedException>(() => KindPlatform.From("Linux", Architecture.X86));

    [Fact]
    public void FromRejectsUnsupportedOperatingSystem() => Should.Throw<PlatformNotSupportedException>(() => KindPlatform.From("FreeBSD", Architecture.X64));
}
