namespace UnifiSmoobuTool.Core.Models;

/// <summary>Manual bookings flow through the same reservation-processing pipeline as Smoobu
/// bookings, so each is given a synthetic <see cref="Reservation.Id"/> offset well above any real
/// Smoobu booking id - keeping the two id spaces from ever colliding without needing a bigger
/// refactor to a generic "reservation source" abstraction throughout the app.</summary>
public static class ManualBookingReservationId
{
    private const long Offset = 1_000_000_000_000L;

    public static long ToReservationId(long manualBookingId) => Offset + manualBookingId;

    public static bool TryGetManualBookingId(long reservationId, out long manualBookingId)
    {
        if (reservationId >= Offset)
        {
            manualBookingId = reservationId - Offset;
            return true;
        }

        manualBookingId = 0;
        return false;
    }
}
