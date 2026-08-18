namespace UnifiSmoobuTool.Core.Models;

/// <summary>Whether automated guest messaging (arrival requests, clarification requests,
/// confirmations) is enabled for a given booking channel/platform (e.g. "Airbnb",
/// "Booking.com", "Direct"). Channels are registered automatically the first time a reservation
/// from that channel is synced, defaulting to enabled.</summary>
public sealed record ChannelMessagingSetting
{
    public required string ChannelName { get; init; }
    public bool Enabled { get; init; } = true;
}
