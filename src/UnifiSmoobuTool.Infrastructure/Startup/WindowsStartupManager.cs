using Microsoft.Win32;

namespace UnifiSmoobuTool.Infrastructure.Startup;

/// <summary>
/// Registers/unregisters the app to launch automatically when the user logs in, via the
/// per-user "Run" registry key (HKEY_CURRENT_USER, so no admin rights are needed). Only works for
/// an installed (Velopack-packaged) copy of the app - there's no stable, update-surviving exe path
/// to register when running unpackaged (e.g. via `dotnet run`).
/// </summary>
public static class WindowsStartupManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "UnifiSmoobuTool";

    public static bool IsSupported => GetInstalledExePath() is not null;

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var exePath = GetInstalledExePath()
            ?? throw new InvalidOperationException(
                "Can't register for Windows startup: this only works for an installed copy of the app.");

        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    private static string? GetInstalledExePath()
    {
        var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var rootDir = Directory.GetParent(currentDir)?.FullName;
        if (rootDir is null)
        {
            return null;
        }

        // Velopack's stub launcher lives one directory above "current\" and stays at a stable
        // path across updates (unlike the versioned "current\...exe" it launches internally).
        var stubPath = Path.Combine(rootDir, "UnifiSmoobuTool.exe");
        return File.Exists(stubPath) ? stubPath : null;
    }
}
