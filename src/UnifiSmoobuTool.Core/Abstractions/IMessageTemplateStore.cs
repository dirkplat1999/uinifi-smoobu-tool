using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IMessageTemplateStore
{
    Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken ct = default);

    Task SaveAsync(MessageTemplate template, CancellationToken ct = default);

    Task DeleteAsync(string languageCode, MessageTemplateKind kind, CancellationToken ct = default);
}
