namespace Kind.NET;

internal static class KindArgumentBuilder
{
    public static IReadOnlyList<string> Create(KindClusterOptions o)
    {
        var a = new List<string> { "create", "cluster", "--name", o.Name };
        Add(a, "--image", o.Image); Add(a, "--config", o.ConfigPath); Add(a, "--kubeconfig", o.KubeConfigPath);
        if (o.Wait is not null) { a.Add("--wait"); a.Add(ToDuration(o.Wait.Value)); }
        if (o.Retain) a.Add("--retain");
        return a;
    }

    public static IReadOnlyList<string> Delete(KindDeleteClusterOptions o)
    {
        var a = new List<string> { "delete", "cluster", "--name", o.Name };
        Add(a, "--kubeconfig", o.KubeConfigPath); return a;
    }

    public static IReadOnlyList<string> ExportKubeConfig(string name, string? path, bool internalAddress)
    {
        var a = new List<string> { "export", "kubeconfig", "--name", name };
        Add(a, "--kubeconfig", path); if (internalAddress) a.Add("--internal"); return a;
    }

    public static IReadOnlyList<string> LoadDockerImages(string name, IEnumerable<string> images, IEnumerable<string>? nodes)
    {
        var a = new List<string> { "load", "docker-image" }; a.AddRange(images); Add(a, "--name", name);
        AddMany(a, "--nodes", nodes); return a;
    }

    public static IReadOnlyList<string> LoadImageArchive(string name, string archive, IEnumerable<string>? nodes)
    {
        var a = new List<string> { "load", "image-archive", archive }; Add(a, "--name", name); AddMany(a, "--nodes", nodes); return a;
    }

    public static IReadOnlyList<string> BuildNodeImage(KindBuildNodeImageOptions o)
    {
        var a = new List<string> { "build", "node-image" }; if (o.KubernetesSource is not null) a.Add(o.KubernetesSource!);
        Add(a, "--base-image", o.BaseImage); Add(a, "--image", o.Image); Add(a, "--type", o.Type); return a;
    }

    private static void Add(List<string> a, string name, string? value) { if (!string.IsNullOrWhiteSpace(value)) { a.Add(name); a.Add(value!); } }
    private static void AddMany(List<string> a, string name, IEnumerable<string>? values) { if (values is not null) foreach (var value in values) Add(a, name, value); }
    private static string ToDuration(TimeSpan value) => value.TotalHours >= 1 ? $"{value.TotalHours:0.###}h" : value.TotalMinutes >= 1 ? $"{value.TotalMinutes:0.###}m" : $"{value.TotalSeconds:0.###}s";
}
