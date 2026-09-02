namespace UnifiSmoobuTool.Core.Models;

/// <summary>A booking entered by hand instead of coming from Smoobu - e.g. a direct guest with no
/// listing-platform booking at all. Flows through the same access-provisioning pipeline as a Smoobu
/// reservation, but the guest-info request is emailed (there's no Smoobu message thread to use),
/// and since a reply can't be detected automatically, it always needs manual review to enter what
/// the guest replied with.</summary>
public sealed record ManualBooking
{
    public required long Id { get; init; }
    public required int ApartmentId { get; init; }
    public required string ApartmentName { get; init; }
    public required string GuestFirstName { get; init; }
    public required string GuestLastName { get; init; }
    public required string GuestEmail { get; init; }
    public string? GuestLanguage { get; init; }
    public required DateOnly Arrival { get; init; }
    public required DateOnly Departure { get; init; }
    public bool Cancelled { get; init; }
}
