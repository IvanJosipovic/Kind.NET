using System.Runtime.InteropServices;

namespace Kind.NET.Scenarios;

internal static class NativeSignalChecks
{
    // The six supported RIDs use 64-bit processes. Linux glibc's sigaction
    // contains a handler, a 1024-bit signal mask, flags, and a restorer.
    [StructLayout(LayoutKind.Sequential, Size = 128)]
    private struct SignalMask { }

    [StructLayout(LayoutKind.Sequential)]
    private struct SignalAction
    {
        internal nint Handler;
        internal SignalMask Mask;
        internal int Flags;
        internal nint Restorer;
    }

    internal static void RequireChildSignalAlternateStack()
    {
        if (!OperatingSystem.IsLinux()) return;
        const int sigChld = 17;
        const int saOnStack = 0x08000000;
        if (GetSignalAction(sigChld, 0, out var action) != 0)
            throw new InvalidOperationException("Could not inspect the child-process signal handler.");
        if ((action.Flags & saOnStack) == 0)
            throw new InvalidOperationException("The managed SIGCHLD handler must use SA_ONSTACK before Kind starts concurrent child processes.");
    }

    [DllImport("libc", EntryPoint = "sigaction", SetLastError = true)]
    private static extern int GetSignalAction(int signal, nint newAction, out SignalAction oldAction);
}
