using System.Runtime.InteropServices;

#pragma warning disable CA2249

namespace Kind.NET;

/// <summary>Describes an operating-system and architecture-specific Kind executable.</summary>
public readonly record struct KindPlatform(string Rid, string FileName)
{
    /// <summary>Gets the platform of the current process.</summary>
    public static KindPlatform Current
    {
        get
        {
            var os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
                throw new PlatformNotSupportedException($"Kind.NET does not support {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}.");

            return From(os, RuntimeInformation.ProcessArchitecture);
        }
    }

    internal static KindPlatform From(string osDescription, Architecture architecture)
    {
        var os = osDescription.Equals("win", StringComparison.OrdinalIgnoreCase) || osDescription.IndexOf("Windows", StringComparison.OrdinalIgnoreCase) >= 0 ? "win" :
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
