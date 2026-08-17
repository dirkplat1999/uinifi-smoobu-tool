using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IWebhookConfigStore
{
    Task<IReadOnlyList<WebhookConfig>> GetAllAsync(CancellationToken ct = default);

    Task<IReadOnlyList<WebhookConfig>> GetForApartmentAsync(
        int apartmentId, AutomationTrigger trigger, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookConfig>> GetErrorWebhooksAsync(CancellationToken ct = default);

    Task SaveAsync(WebhookConfig config, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
