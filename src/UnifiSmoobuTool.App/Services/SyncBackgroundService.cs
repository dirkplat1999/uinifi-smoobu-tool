using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Services;

namespace UnifiSmoobuTool.App.Services;

/// <summary>Runs <see cref="BookingSyncOrchestrator"/> on a loop for as long as the app is running
/// (including minimized to the tray), reading the polling interval from settings on every cycle so
/// a change in Settings takes effect on the next run without an app restart.</summary>
public sealed class SyncBackgroundService : BackgroundService
{
    private readonly BookingSyncOrchestrator _orchestrator;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<SyncBackgroundService> _logger;

    public SyncBackgroundService(
        BookingSyncOrchestrator orchestrator,
        IAppSettingsStore settingsStore,
        ILogger<SyncBackgroundService> logger)
    {
        _orchestrator = orchestrator;
        _settingsStore = settingsStore;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _orchestrator.RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Unhandled error during a booking sync cycle.");
            }

            var settings = await _settingsStore.GetAsync(stoppingToken).ConfigureAwait(false);
            var delay = TimeSpan.FromMinutes(Math.Max(1, settings.PollingIntervalMinutes));

            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }
    }
}
