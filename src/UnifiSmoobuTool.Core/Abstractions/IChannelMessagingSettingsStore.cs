using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IChannelMessagingSettingsStore
{
    Task<IReadOnlyList<ChannelMessagingSetting>> GetAllAsync(CancellationToken ct = default);

    Task<ChannelMessagingSetting?> GetAsync(string channelName, CancellationToken ct = default);

    Task SaveAsync(ChannelMessagingSetting setting, CancellationToken ct = default);

    /// <summary>Registers a channel as enabled if it isn't already known - a no-op otherwise, so
    /// discovering the same channel repeatedly during sync never resets a user's existing choice.</summary>
    Task EnsureRegisteredAsync(string channelName, CancellationToken ct = default);
}
