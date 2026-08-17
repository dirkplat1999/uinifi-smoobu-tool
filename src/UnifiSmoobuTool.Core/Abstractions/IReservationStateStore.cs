using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IReservationStateStore
{
    Task<ReservationProcessingState?> GetAsync(long reservationId, CancellationToken ct = default);

    Task SaveAsync(ReservationProcessingState state, CancellationToken ct = default);

    Task<IReadOnlyList<ReservationProcessingState>> GetAllNeedingManualReviewAsync(CancellationToken ct = default);
}
