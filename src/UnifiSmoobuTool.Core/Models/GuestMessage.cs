namespace UnifiSmoobuTool.Core.Models;

public sealed class GuestMessage
{
    public required long ReservationId { get; init; }
    public required string Text { get; init; }
    public required DateTimeOffset SentAt { get; init; }
    public required MessageDirection Direction { get; init; }
}

public enum MessageDirection
{
    HostToGuest,
    GuestToHost,
}
