namespace UnifiSmoobuTool.Core.Models;

/// <summary>
/// Persisted automation progress for a single Smoobu reservation, so restarts never re-send
/// messages or re-provision access that already happened.
/// </summary>
public sealed class ReservationProcessingState
{
    public required long ReservationId { get; init; }

    public DateTimeOffset? RequestMessageSentAt { get; set; }

    public DateTimeOffset? GuestReplyReceivedAt { get; set; }
    public string? ParsedLicensePlate { get; set; }
    public string? ParsedPinCode { get; set; }
    public bool NeedsManualReview { get; set; }

    public DateTimeOffset? AccessCreatedAt { get; set; }
    public string? UnifiVisitorId { get; set; }

    public DateTimeOffset? AccessRevokedAt { get; set; }

    public DateTimeOffset? ArrivalDayNotifiedAt { get; set; }
}
