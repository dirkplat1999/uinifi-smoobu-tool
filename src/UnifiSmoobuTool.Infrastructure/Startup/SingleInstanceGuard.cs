using System.Runtime.InteropServices;

namespace UnifiSmoobuTool.Infrastructure.Startup;

/// <summary>
/// Prevents more than one copy of the app from running at once. Backed by a named OS mutex
/// (session-local, so this doesn't reach across users on a shared machine) rather than a lock
/// file or port, since a mutex is automatically released even if a previous instance crashed
/// without cleaning up. When a second launch is detected, the caller should bring the existing
/// instance's window to the front (<see cref="ActivateExistingInstance"/>) and exit immediately.
/// </summary>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string MutexName = "UnifiSmoobuTool-SingleInstance-8F3D2A1C-5B6E-4D7F-9A0B-1C2D3E4F5A6B";

    private readonly Mutex _mutex;
    private bool _disposed;

    /// <summary>True if this is the only running instance; false if another one already holds the mutex.</summary>
    public bool IsFirstInstance { get; }

    public SingleInstanceGuard()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
    }

    /// <summary>Finds the already-running instance's main window by its exact title and restores/
    /// foregrounds it - works even if it's currently hidden in the system tray.</summary>
    public static void ActivateExistingInstance(string windowTitle)
    {
        var hwnd = NativeMethods.FindWindow(null, windowTitle);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        NativeMethods.ShowWindow(hwnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetForegroundWindow(hwnd);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (IsFirstInstance)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _disposed = true;
    }

    private static class NativeMethods
    {
        public const int SW_RESTORE = 9;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
