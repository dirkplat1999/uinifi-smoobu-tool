namespace UnifiSmoobuTool.Core.Models;

/// <summary>
/// A user-configured outbound automation webhook. When <see cref="ApartmentId"/> is null this is a
/// global error/alerting webhook rather than a per-apartment guest-facing automation.
/// </summary>
public sealed record WebhookConfig
{
    public required Guid Id { get; init; }
    public int? ApartmentId { get; init; }
    public required string Name { get; init; }
    public required AutomationTrigger Trigger { get; init; }
    public required WebhookMethod Method { get; init; }
    public required string Url { get; init; }
    public string? PayloadTemplate { get; init; }
    public bool Enabled { get; init; } = true;
}

public enum WebhookMethod
{
    Get,
    Post,
}

public enum AutomationTrigger
{
    ReservationCreated,
    GuestReplyReceived,
    AccessGranted,
    ArrivalDay,
    AccessRevoked,
    ErrorOccurred,
}
