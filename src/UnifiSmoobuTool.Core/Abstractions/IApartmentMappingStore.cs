using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IApartmentMappingStore
{
    Task<IReadOnlyList<ApartmentAccessMapping>> GetAllAsync(CancellationToken ct = default);

    Task<ApartmentAccessMapping?> GetAsync(int apartmentId, CancellationToken ct = default);

    Task SaveAsync(ApartmentAccessMapping mapping, CancellationToken ct = default);
}
