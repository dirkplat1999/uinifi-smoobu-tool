using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface ISmoobuClient
{
    Task<IReadOnlyList<Apartment>> GetApartmentsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<Reservation>> GetReservationsAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<Reservation?> GetReservationAsync(long reservationId, CancellationToken ct = default);

    Task<IReadOnlyList<GuestMessage>> GetMessagesAsync(long reservationId, CancellationToken ct = default);

    Task SendMessageToGuestAsync(long reservationId, string message, CancellationToken ct = default);
}
