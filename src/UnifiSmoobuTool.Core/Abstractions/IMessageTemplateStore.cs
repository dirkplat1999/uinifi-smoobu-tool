using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IMessageTemplateStore
{
    Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken ct = default);

    Task SaveAsync(MessageTemplate template, CancellationToken ct = default);

    Task DeleteAsync(string languageCode, MessageTemplateKind kind, CancellationToken ct = default);

    /// <summary>Discards every template and re-seeds the built-in defaults - used by the "Reset to
    /// defaults" button, so it's a deliberate, whole-table replace rather than a per-row upsert.</summary>
    Task ResetToDefaultsAsync(CancellationToken ct = default);
}
