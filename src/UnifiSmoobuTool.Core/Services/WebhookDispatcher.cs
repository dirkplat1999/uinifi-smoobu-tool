using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Services;

public sealed class WebhookDispatcher
{
    private readonly IWebhookSender _sender;

    public WebhookDispatcher(IWebhookSender sender)
    {
        _sender = sender ?? throw new ArgumentNullException(nameof(sender));
    }

    public async Task DispatchAsync(
        WebhookConfig config,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(placeholders);

        if (!config.Enabled)
        {
            return;
        }

        // URL placeholders are substituted with URL-encoded values (many GET-triggered targets,
        // e.g. Homey flow webhooks or simple display endpoints, only support query-string data and
        // have no request body), while the payload template keeps raw values for JSON/form bodies.
        var urlEncodedPlaceholders = placeholders.ToDictionary(
            kv => kv.Key, kv => Uri.EscapeDataString(kv.Value), StringComparer.OrdinalIgnoreCase);
        string url = TemplateRenderer.Render(config.Url, urlEncodedPlaceholders);

        string? payload = config.PayloadTemplate is null
            ? null
            : TemplateRenderer.Render(config.PayloadTemplate, placeholders);

        await _sender.SendAsync(url, config.Method, payload, ct).ConfigureAwait(false);
    }

    public async Task DispatchAllAsync(
        IEnumerable<WebhookConfig> configs,
        IReadOnlyDictionary<string, string> placeholders,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(configs);
        foreach (var config in configs)
        {
            await DispatchAsync(config, placeholders, ct).ConfigureAwait(false);
        }
    }
}
