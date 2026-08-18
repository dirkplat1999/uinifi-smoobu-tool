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

    /// <summary>Set when a clarification message was auto-sent because the guest's reply couldn't
    /// be confidently read. Gates a single automatic re-check of a follow-up reply.</summary>
    public DateTimeOffset? ClarificationRequestedAt { get; set; }

    /// <summary>Set when a confirmation/thank-you message was auto-sent because the guest's reply
    /// was read clearly.</summary>
    public DateTimeOffset? ConfirmationSentAt { get; set; }

    public DateTimeOffset? AccessCreatedAt { get; set; }
    public string? UnifiVisitorId { get; set; }

    public DateTimeOffset? AccessRevokedAt { get; set; }

    public DateTimeOffset? ArrivalDayNotifiedAt { get; set; }
}
