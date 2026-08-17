using System.Text.Json.Serialization;

namespace UnifiSmoobuTool.Infrastructure.UnifiAccess;

// DTOs mirror the official UniFi Access Open API reference (Section 4 "Visitor" and Section 7.1
// "Fetch Door Group Topology"), downloaded from assets.identity.ui.com/unifi-access/api_reference.pdf
// while building this client.

internal sealed class UnifiApiEnvelope<T>
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("msg")]
    public string? Msg { get; set; }
}

internal sealed class UnifiDoorGroupTopologyDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resource_topologies")]
    public List<UnifiResourceTopologyDto>? ResourceTopologies { get; set; }
}

internal sealed class UnifiResourceTopologyDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resources")]
    public List<UnifiDoorResourceDto>? Resources { get; set; }
}

internal sealed class UnifiDoorResourceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

internal sealed class UnifiVisitorDataDto
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

internal sealed class UnifiResourceRequestDto
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("type")]
    public required string Type { get; set; }
}

internal sealed class UnifiCreateVisitorRequestDto
{
    [JsonPropertyName("first_name")]
    public required string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public required string LastName { get; set; }

    [JsonPropertyName("start_time")]
    public long StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public long EndTime { get; set; }

    [JsonPropertyName("visit_reason")]
    public string VisitReason { get; set; } = "Others";

    [JsonPropertyName("resources")]
    public List<UnifiResourceRequestDto>? Resources { get; set; }
}

internal sealed class UnifiUpdateVisitorRequestDto
{
    [JsonPropertyName("start_time")]
    public long? StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public long? EndTime { get; set; }
}

internal sealed class UnifiPinCodeRequestDto
{
    [JsonPropertyName("pin_code")]
    public required string PinCode { get; set; }
}
