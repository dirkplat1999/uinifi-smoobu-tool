using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Abstractions;

public interface IUnifiAccessClient
{
    Task<IReadOnlyList<UnifiResourceRef>> GetDoorGroupTopologyAsync(CancellationToken ct = default);

    Task<string> CreateVisitorAsync(CreateVisitorRequest request, CancellationToken ct = default);

    Task UpdateVisitorAsync(string visitorId, UpdateVisitorRequest request, CancellationToken ct = default);

    Task DeleteVisitorAsync(string visitorId, bool force, CancellationToken ct = default);

    Task AssignPinCodeAsync(string visitorId, string pinCode, CancellationToken ct = default);

    Task AssignLicensePlatesAsync(string visitorId, IReadOnlyList<string> plates, CancellationToken ct = default);
}

public sealed class CreateVisitorRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public string VisitReason { get; init; } = "Others";
    public IReadOnlyList<UnifiResourceRef> Resources { get; init; } = Array.Empty<UnifiResourceRef>();
}

public sealed class UpdateVisitorRequest
{
    public DateTimeOffset? StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
}
