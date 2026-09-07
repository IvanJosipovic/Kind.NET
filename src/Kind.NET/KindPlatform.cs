using System.Runtime.InteropServices;

#pragma warning disable CA2249

namespace Kind.NET;

/// <summary>Describes an operating-system and architecture-specific Kind executable.</summary>
public readonly record struct KindPlatform(string Rid, string FileName)
{
    /// <summary>Gets the platform of the current process.</summary>
    public static KindPlatform Current => From(RuntimeInformation.OSDescription, RuntimeInformation.ProcessArchitecture);

    internal static KindPlatform From(string osDescription, Architecture architecture)
    {
        var os = osDescription.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0 ? "win" :
            osDescription.IndexOf("Darwin", StringComparison.OrdinalIgnoreCase) >= 0 || osDescription.IndexOf("Mac", StringComparison.OrdinalIgnoreCase) >= 0 ? "osx" :
            osDescription.IndexOf("Linux", StringComparison.OrdinalIgnoreCase) >= 0 ? "linux" : null;
        var arch = architecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            _ => null
        };

        if (os is null || arch is null)
            throw new PlatformNotSupportedException($"Kind.NET does not support {osDescription} / {architecture}.");

        return new KindPlatform($"{os}-{arch}", os == "win" ? "kind.exe" : "kind");
    }
}
