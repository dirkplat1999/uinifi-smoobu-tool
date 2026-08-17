using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Services;

namespace UnifiSmoobuTool.Infrastructure.Notifications;

/// <summary>
/// Fans a sync-loop error out to the configured SMTP address and error webhooks (e.g. a Homey Pro
/// flow trigger), throttled so a flapping API doesn't spam either channel: at most one alert per
/// distinct component+message combination per hour.
/// </summary>
public sealed class CompositeErrorNotifier : IErrorNotifier
{
    private static readonly TimeSpan ThrottleWindow = TimeSpan.FromHours(1);

    private readonly IAppSettingsStore _settingsStore;
    private readonly IWebhookConfigStore _webhookStore;
    private readonly WebhookDispatcher _webhookDispatcher;
    private readonly SmtpAlerter _smtpAlerter;
    private readonly IClock _clock;
    private readonly ILogger<CompositeErrorNotifier> _logger;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastSentAt = new();

    public CompositeErrorNotifier(
        IAppSettingsStore settingsStore,
        IWebhookConfigStore webhookStore,
        WebhookDispatcher webhookDispatcher,
        SmtpAlerter smtpAlerter,
        IClock clock,
        ILogger<CompositeErrorNotifier> logger)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _webhookStore = webhookStore ?? throw new ArgumentNullException(nameof(webhookStore));
        _webhookDispatcher = webhookDispatcher ?? throw new ArgumentNullException(nameof(webhookDispatcher));
        _smtpAlerter = smtpAlerter ?? throw new ArgumentNullException(nameof(smtpAlerter));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task NotifyErrorAsync(string component, string message, Exception? exception, CancellationToken ct = default)
    {
        var dedupeKey = $"{component}:{message}";
        var now = _clock.UtcNow;

        if (_lastSentAt.TryGetValue(dedupeKey, out var last) && now - last < ThrottleWindow)
        {
            _logger.LogDebug("Suppressed duplicate error alert for {DedupeKey} (throttled).", dedupeKey);
            return;
        }
        _lastSentAt[dedupeKey] = now;

        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);
        var fullMessage = exception is null ? message : $"{message}\n\n{exception}";

        if (settings.Smtp is not null)
        {
            await _smtpAlerter.SendAsync(settings.Smtp, $"UniFi Smoobu Tool error: {component}", fullMessage, ct).ConfigureAwait(false);
        }

        var webhooks = await _webhookStore.GetErrorWebhooksAsync(ct).ConfigureAwait(false);
        if (webhooks.Count > 0)
        {
            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["component"] = component,
                ["message"] = message,
                ["timestamp"] = now.ToString("O"),
            };
            await _webhookDispatcher.DispatchAllAsync(webhooks, placeholders, ct).ConfigureAwait(false);
        }
    }
}
