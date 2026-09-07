namespace Kind.NET;

internal static class KindExecutableResolver
{
    public static string Resolve(KindClientOptions options, KindPlatform platform, string? baseDirectory = null)
    {
        if (!string.IsNullOrWhiteSpace(options.ExecutablePath)) return options.ExecutablePath!;
        if (options.UseBundledExecutable)
        {
            var root = baseDirectory ?? AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(root, platform.FileName),
                Path.Combine(root, "runtimes", platform.Rid, "native", platform.FileName)
            };
            var path = candidates.FirstOrDefault(File.Exists);
            if (path is not null)
            {
                EnsureUnixExecutable(path, platform);
                return path;
            }
        }

        return platform.FileName;
    }

    private static void EnsureUnixExecutable(string path, KindPlatform platform)
    {
        if (platform.FileName == "kind.exe") return;
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "chmod",
                Arguments = $"+x \"{path.Replace("\\\"", "\\\\\"")}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // The command will provide the actionable error if chmod is unavailable.
        }
    }
}
