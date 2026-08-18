using System.Text.Json.Serialization;

namespace UnifiSmoobuTool.Infrastructure.Smoobu;

// DTOs mirror Smoobu's documented REST API field naming (kebab-case) as of the current public
// docs at docs.smoobu.com. Field names here are best-effort against the docs and have not been
// exercised against a live account yet (no API key was available while building this client) -
// verify against real responses (the Log Viewer will show the raw parsed values) once a Smoobu
// API key is configured, and adjust the [JsonPropertyName] attributes below if anything doesn't
// map as expected.

internal sealed class SmoobuApartmentListDto
{
    [JsonPropertyName("apartments")]
    public List<SmoobuApartmentDto>? Apartments { get; set; }
}

internal sealed class SmoobuApartmentDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class SmoobuChannelDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

internal sealed class SmoobuReservationListDto
{
    [JsonPropertyName("bookings")]
    public List<SmoobuReservationDto>? Bookings { get; set; }

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("page_count")]
    public int PageCount { get; set; } = 1;
}

internal sealed class SmoobuReservationDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("arrival")]
    public DateOnly Arrival { get; set; }

    [JsonPropertyName("departure")]
    public DateOnly Departure { get; set; }

    [JsonPropertyName("apartment")]
    public SmoobuApartmentDto? Apartment { get; set; }

    [JsonPropertyName("channel")]
    public SmoobuChannelDto? Channel { get; set; }

    [JsonPropertyName("firstname")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastname")]
    public string? LastName { get; set; }

    [JsonPropertyName("guest-name")]
    public string? GuestName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("is-cancelled")]
    public bool? IsCancelled { get; set; }
}

internal sealed class SmoobuMessageListDto
{
    [JsonPropertyName("messages")]
    public List<SmoobuMessageDto>? Messages { get; set; }
}

internal sealed class SmoobuMessageDto
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("created-at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("from-guest")]
    public bool? FromGuest { get; set; }
}

internal sealed class SmoobuSendMessageRequestDto
{
    [JsonPropertyName("message")]
    public required string Message { get; set; }
}
