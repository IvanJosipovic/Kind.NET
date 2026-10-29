# Kind.NET

[![NuGet](https://img.shields.io/nuget/vpre/Kind.NET.svg?style=flat-square)](https://www.nuget.org/packages/Kind.NET)
[![Downloads](https://img.shields.io/nuget/dt/Kind.NET.svg?style=flat-square)](https://www.nuget.org/packages/Kind.NET)
[![codecov](https://codecov.io/gh/IvanJosipovic/Kind.NET/graph/badge.svg?token=nZGzjHvaDh)](https://codecov.io/gh/IvanJosipovic/Kind.NET)

Kind.NET is a typed .NET client for Kubernetes in Docker. It calls Kind's Go library through a native shim. It does not start the Kind CLI executable.

```sh
dotnet add package Kind.NET
```

Native libraries target Windows, Linux, and macOS on x64 and ARM64. Cluster operations still require a supported container provider (Docker, Podman, or nerdctl) and a working Linux container environment. Platform support for the native library does not imply that every container provider or node-image build works on every OS.

```csharp
using Kind.NET;

IKindClient kind = new KindClient(new KindClientOptions { Provider = "docker" });
kind.CreateCluster(new KindClusterOptions("demo", Wait: TimeSpan.FromMinutes(3)));
try
{
    var nodes = kind.GetNodes("demo");
    var kubeconfig = kind.GetKubeConfig("demo");
    kind.ExportKubeConfig("demo", "demo.kubeconfig");
}
finally
{
    kind.DeleteCluster(new KindDeleteClusterOptions("demo"));
}
```

| Area | Methods |
| --- | --- |
| Clusters | `CreateCluster`, `DeleteCluster`, `GetClusters` |
| Nodes | `GetNodes`, `GetAllNodes` |
| Kubeconfig | `GetKubeConfig`, `GetInternalKubeConfig`, `ExportKubeConfig`, `ExportInternalKubeConfig` |
| Diagnostics | `ExportLogs`, `GetVersion` |
| Images | `LoadDockerImages`, `LoadImageArchive`, `BuildNodeImage` |

Methods with an optional cluster name default to `kind`. `GetAllNodes` explicitly queries every cluster. `ExportLogs` returns its output directory and creates a temporary directory when none is supplied. `GetVersion` returns the bundled Kind semantic version.

`KindClientOptions.Provider` selects `docker`, `podman`, or `nerdctl`. If omitted, `KIND_EXPERIMENTAL_PROVIDER` applies, followed by Kind's provider detection. `LoadDockerImages` reads the local Docker image store even when the target cluster uses another provider; use `LoadImageArchive` for an existing archive. Node-image builds use Kind's upstream Docker-based build implementation.

Failures from Kind throw `KindActionException`. Native library loading failures retain the standard .NET interop exceptions.

## NativeAOT and publishing

The library uses source-generated JSON serialization with reflection disabled. Publish for the target runtime:

```sh
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishAot=true
```

Keep the matching native library with the published application. NuGet consumers receive the RID-specific native asset; project-reference builds also carry the `runtimes/<rid>/native` layout. Single-file embedding of the native library is not configured; distribute the native library alongside the application.

## Migration from the CLI wrapper

This is a breaking API change matching Helm.NET's synchronous native client design:

- Replace typed `*Async` calls with their synchronous equivalents; mutating operations return `void` instead of process output.
- Use `IKindClient` for application abstraction and `KindActionException` for action failures.
- Raw `ExecuteAsync` and shell completion generation are removed. Invoke a separately installed CLI explicitly if your application needs those CLI features.
- `ExecutablePath`, `UseBundledExecutable`, `WorkingDirectory`, `Environment`, and `CommandTimeout` are removed. Configure provider selection through `Provider`, pass explicit paths in operation options, and configure container tools in the application environment.
- Native calls do not provide cancellation tokens or a general operation timeout. `KindClusterOptions.Wait` controls Kind's control-plane readiness wait; it is not a timeout for the whole native operation.
- Replace `ExportDefaultKubeConfigAsync(path)` with `ExportKubeConfig(path: path)` and corresponding internal kubeconfig/log calls with named `path` arguments.

## Development

The pinned Kind dependency and Go shim are in `native/kind-shim`. Run from that directory:

```sh
go test -cover ./...
go build -buildmode=c-shared -trimpath -buildvcs=false -ldflags='-s -w -buildid=' -o ../../src/Kind.NET/runtimes/linux-x64/native/libkindshim.so .
```

Use the matching library filename and Go target on Windows/macOS. The generated `.h` header is a build verification artifact and is not packaged. The native update workflow builds all six RIDs, verifies `KindExecuteJSON` and `KindFree`, and opens a PR containing the dependency lockfiles and binaries.

Managed unit tests live in `tests/Kind.NET.Tests`. Shared public API scenarios live in `tests/Kind.NET.Tests.E2E.Shared` and run through managed and NativeAOT xUnit hosts:

```sh
dotnet test --project tests/Kind.NET.Tests/Kind.NET.Tests.csproj
dotnet test --project tests/Kind.NET.Tests.E2E/Kind.NET.Tests.E2E.csproj
dotnet publish tests/Kind.NET.Tests.E2E.AOT/Kind.NET.Tests.E2E.AOT.csproj -c Release -f net11.0 -r linux-x64 --self-contained true -p:PublishAot=true -o artifacts/aot
./artifacts/aot/Kind.NET.Tests.E2E.AOT
```

Without `KIND_TEST_CLUSTER=true`, both E2E hosts run native-loading, version, and error propagation checks. Set it to `true` to run cluster lifecycle, kubeconfig, logs, Docker image/archive import, and multiple-cluster scenarios; these require Docker and kubectl. `KIND_TEST_BUILD_NODE_IMAGE=true` additionally enables the expensive release node-image build on Linux. Go lifecycle tests use `KIND_SHIM_TEST_CLUSTER=true`.

CI runs the NativeAOT smoke host for both target frameworks on all six RIDs, plus managed and NativeAOT lifecycle tests on Linux. Releases depend on these checks.
