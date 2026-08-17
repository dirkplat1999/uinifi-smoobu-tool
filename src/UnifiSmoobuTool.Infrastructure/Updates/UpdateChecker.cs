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
