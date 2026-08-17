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
    public required DateOnly Arrival { get; init; }
    public required DateOnly Departure { get; init; }
    public ReservationStatus Status { get; init; } = ReservationStatus.Confirmed;

    public string GuestFullName => $"{GuestFirstName} {GuestLastName}".Trim();
}

public enum ReservationStatus
{
    Confirmed,
    Cancelled,
    Tentative,
}
