using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IWebhookSender
{
    Task SendAsync(string url, WebhookMethod method, string? jsonOrFormPayload, CancellationToken ct = default);
}
