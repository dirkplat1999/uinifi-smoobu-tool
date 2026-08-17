namespace UnifiSmoobuTool.Core.Models;

public sealed class ApartmentAccessMapping
{
    public required int SmoobuApartmentId { get; init; }
    public required string ApartmentName { get; init; }
    public List<UnifiResourceRef> UnifiResources { get; init; } = new();
}

/// <summary>A UniFi Access door or door_group that can be assigned to a visitor.</summary>
public sealed class UnifiResourceRef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
}
