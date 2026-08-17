using System.Text;
using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Notifications;

/// <summary>
/// Fires the user-configured universal webhooks (per-apartment automations and error alerts).
/// A failure here is logged but never thrown, so a broken smart-display webhook can't take down
/// the booking sync loop.
/// </summary>
public sealed class HttpWebhookSender : IWebhookSender
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpWebhookSender> _logger;

    public HttpWebhookSender(HttpClient httpClient, ILogger<HttpWebhookSender> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task SendAsync(string url, WebhookMethod method, string? jsonOrFormPayload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("Skipped a webhook with no URL configured.");
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(method == WebhookMethod.Get ? HttpMethod.Get : HttpMethod.Post, url);
            if (method == WebhookMethod.Post && !string.IsNullOrEmpty(jsonOrFormPayload))
            {
                var contentType = LooksLikeJson(jsonOrFormPayload) ? "application/json" : "text/plain";
                request.Content = new StringContent(jsonOrFormPayload, Encoding.UTF8, contentType);
            }

            using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Webhook call to {Url} returned {StatusCode}.", url, (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook call to {Url} failed.", url);
        }
    }

    private static bool LooksLikeJson(string payload)
    {
        var trimmed = payload.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
