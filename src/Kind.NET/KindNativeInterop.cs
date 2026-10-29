using System.Runtime.InteropServices;

namespace Kind.NET;

internal interface IKindNativeApi
{
    nint Execute(nint input);
    string? GetString(nint value);
    void Free(nint value);
}

internal static class KindNativeInterop
{
    static KindNativeInterop()
    {
        NativeLibrary.SetDllImportResolver(typeof(KindNativeInterop).Assembly, ResolveLibrary);
    }

    internal static IKindNativeApi DefaultApi { get; } = new NativeKindApi();

    private static nint ResolveLibrary(
        string libraryName,
        System.Reflection.Assembly _assembly,
        DllImportSearchPath? _searchPath)
    {
        if (!string.Equals(libraryName, "kindshim", StringComparison.Ordinal))
            return 0;

        var libraryPath = Path.Combine(
            AppContext.BaseDirectory,
            "runtimes",
            RuntimeInformation.RuntimeIdentifier,
            "native",
            GetLibraryFileName());

        return NativeLibrary.TryLoad(libraryPath, out var handle) ? handle : nint.Zero;
    }

    private static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
            return "kindshim.dll";

        return OperatingSystem.IsMacOS() ? "libkindshim.dylib" : "libkindshim.so";
    }

    internal static string Invoke(byte[] payload, IKindNativeApi nativeApi)
    {
        var input = Marshal.AllocHGlobal(payload.Length + 1);
        try
        {
            Marshal.Copy(payload, 0, input, payload.Length);
            Marshal.WriteByte(input, payload.Length, 0);
            var resultPointer = nativeApi.Execute(input);
            if (resultPointer == 0)
                throw new InvalidOperationException("The Kind native library returned a null response.");

            try
            {
                return nativeApi.GetString(resultPointer) ?? throw new InvalidOperationException("The Kind native library returned an invalid response.");
            }
            finally
            {
                nativeApi.Free(resultPointer);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(input);
        }
    }

    private sealed class NativeKindApi : IKindNativeApi
    {
        public nint Execute(nint input) => NativeMethods.KindExecuteJson(input);
        public string? GetString(nint value) => Marshal.PtrToStringUTF8(value);
        public void Free(nint value) => NativeMethods.KindFree(value);

        private static class NativeMethods
        {
            [DllImport("kindshim", EntryPoint = "KindExecuteJSON", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern nint KindExecuteJson(nint input);

            [DllImport("kindshim", EntryPoint = "KindFree", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
            internal static extern void KindFree(nint value);
        }
    }
}
