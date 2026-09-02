namespace UnifiSmoobuTool.Core.Models;

public sealed record Reservation
{
    public required long Id { get; init; }
    public required int ApartmentId { get; init; }
    public required string ApartmentName { get; init; }
    public required string GuestFirstName { get; init; }
    public required string GuestLastName { get; init; }
    public string? GuestEmail { get; init; }
    public string? GuestPhone { get; init; }
    public string? GuestLanguage { get; init; }

    /// <summary>The booking channel/platform this reservation came from (e.g. "Airbnb",
    /// "Booking.com", "Direct"), as reported by Smoobu. Null when Smoobu doesn't report one.</summary>
    public string? Channel { get; init; }

    public required DateOnly Arrival { get; init; }
    public required DateOnly Departure { get; init; }
    public ReservationStatus Status { get; init; } = ReservationStatus.Confirmed;

    /// <summary>Where this reservation came from. Manual reservations have no inbound messaging
    /// channel - the guest-info request is emailed instead of sent via Smoobu, and since there's
    /// no way to detect a reply automatically, they always land in the manual-review queue for the
    /// host to fill in from the guest's actual email reply.</summary>
    public ReservationSource Source { get; init; } = ReservationSource.Smoobu;

    public string GuestFullName => $"{GuestFirstName} {GuestLastName}".Trim();
}

public enum ReservationStatus
{
    Confirmed,
    Cancelled,
    Tentative,
}

public enum ReservationSource
{
    Smoobu,
    Manual,
}
