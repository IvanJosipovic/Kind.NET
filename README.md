# Kind.NET

[![Nuget](https://img.shields.io/nuget/vpre/Kind.NET.svg?style=flat-square)](https://www.nuget.org/packages/Kind.NET)
[![Nuget)](https://img.shields.io/nuget/dt/Kind.NET.svg?style=flat-square)](https://www.nuget.org/packages/Kind.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Kind.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Kind.NET)

## What is this?

Kind.NET is a cross-platform .NET wrapper for the Kind CLI. The standard NuGet
package bundles the pinned Kind executable for Windows, Linux, and macOS on
x64 and ARM64. A supported container provider (Docker, Podman, or nerdctl) is
still required to create clusters.

```csharp
using Kind.NET;

var kind = new KindClient();
await kind.CreateClusterAsync(new KindClusterOptions(Name: "demo"));
var kubeConfig = await kind.GetKubeConfigAsync("demo");
await kind.DeleteClusterAsync(new KindDeleteClusterOptions("demo"));
```

