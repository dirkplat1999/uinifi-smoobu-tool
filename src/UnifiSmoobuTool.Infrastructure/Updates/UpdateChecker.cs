using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Velopack;
using Velopack.Sources;

namespace UnifiSmoobuTool.Infrastructure.Updates;

/// <summary>Checks dirkplat1999/uinifi-smoobu-tool's GitHub Releases for a newer Velopack package
/// and can download + apply it. No-ops gracefully when running unpackaged (e.g. via `dotnet run`
/// during development), since Velopack only manages installed, packaged copies of the app.</summary>
public sealed class UpdateChecker
{
    private const string RepoUrl = "https://github.com/dirkplat1999/uinifi-smoobu-tool";

    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(ILogger<UpdateChecker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct = default)
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!manager.IsInstalled)
            {
                return UpdateCheckResult.NotInstalled();
            }

            var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            return updateInfo is null
                ? UpdateCheckResult.UpToDate()
                : UpdateCheckResult.UpdateAvailable(updateInfo.TargetFullRelease.Version.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            return UpdateCheckResult.Failed(ex.Message);
        }
    }

    /// <summary>Downloads and applies the update, then restarts the app. Never returns on success.</summary>
    public async Task DownloadAndApplyUpdateAsync(CancellationToken ct = default)
    {
        var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
        var updateInfo = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
        if (updateInfo is null)
        {
            return;
        }

        await manager.DownloadUpdatesAsync(updateInfo, cancelToken: ct).ConfigureAwait(false);
        manager.ApplyUpdatesAndRestart(updateInfo.TargetFullRelease);
    }

    /// <summary>Launches Velopack's own uninstaller (removes program files, shortcuts, and the
    /// registry entry), which needs the app to exit immediately afterward so it can delete files
    /// currently in use. Does not touch the app's local settings/database under %AppData%.</summary>
    public UninstallResult TriggerUninstall()
    {
        try
        {
            var manager = new UpdateManager(new GithubSource(RepoUrl, null, false));
            if (!manager.IsInstalled)
            {
                return UninstallResult.Failure("This feature is only available for the installed version of the app.");
            }

            var currentDir = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var rootDir = Directory.GetParent(currentDir)?.FullName;
            if (rootDir is null)
            {
                return UninstallResult.Failure("Couldn't determine the install directory.");
            }

            var updateExePath = Path.Combine(rootDir, "Update.exe");
            if (!File.Exists(updateExePath))
            {
                return UninstallResult.Failure($"Couldn't find the uninstaller at {updateExePath}.");
            }

            Process.Start(new ProcessStartInfo(updateExePath, "uninstall") { UseShellExecute = true });
            return UninstallResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start the uninstaller.");
            return UninstallResult.Failure(ex.Message);
        }
    }
}

public sealed record UninstallResult(bool Started, string? Error)
{
    public static UninstallResult Success() => new(true, null);
    public static UninstallResult Failure(string error) => new(false, error);
}

public sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Detail)
{
    public static UpdateCheckResult NotInstalled() => new(UpdateCheckStatus.NotInstalled, null);
    public static UpdateCheckResult UpToDate() => new(UpdateCheckStatus.UpToDate, null);
    public static UpdateCheckResult UpdateAvailable(string version) => new(UpdateCheckStatus.UpdateAvailable, version);
    public static UpdateCheckResult Failed(string error) => new(UpdateCheckStatus.Failed, error);
}

public enum UpdateCheckStatus
{
    NotInstalled,
    UpToDate,
    UpdateAvailable,
    Failed,
}
