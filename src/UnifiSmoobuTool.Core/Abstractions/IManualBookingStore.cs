using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IManualBookingStore
{
    Task<IReadOnlyList<ManualBooking>> GetAllAsync(CancellationToken ct = default);

    Task<ManualBooking?> GetAsync(long id, CancellationToken ct = default);

    /// <summary>Inserts a new manual booking and returns its assigned id.</summary>
    Task<long> AddAsync(ManualBooking booking, CancellationToken ct = default);

    Task UpdateAsync(ManualBooking booking, CancellationToken ct = default);

    Task SetCancelledAsync(long id, bool cancelled, CancellationToken ct = default);
}
