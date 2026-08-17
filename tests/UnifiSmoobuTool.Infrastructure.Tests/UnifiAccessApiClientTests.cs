using Microsoft.Extensions.Logging.Abstractions;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Infrastructure.UnifiAccess;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class UnifiAccessApiClientTests
{
    // UnifiAccessApiClient builds its own internal HttpClient (so it can react to host/cert-trust
    // changes at runtime), so these tests exercise it against a local HttpListener-free approach:
    // a loopback fake server is overkill here, so instead we verify request/response contracts via
    // the same FakeHttpMessageHandler technique is not directly wireable (no DI seam for the
    // handler). Instead we verify the DTOs/JSON shape and error mapping through the public surface
    // using a minimal in-process HTTP server.

    private static async Task<(UnifiAccessApiClient Client, TestHttpServer Server)> BuildAsync()
    {
        var server = await TestHttpServer.StartAsync().ConfigureAwait(false);
        var settingsStore = new InMemoryAppSettingsStore
        {
            Settings = new AppSettings
            {
                UnifiAccessHost = server.BaseUrl,
                UnifiAccessApiToken = "test-token",
                UnifiAccessTrustAnySslCert = true,
            },
        };
        var client = new UnifiAccessApiClient(settingsStore, NullLogger<UnifiAccessApiClient>.Instance);
        return (client, server);
    }

    [Fact]
    public async Task CreateVisitorAsync_SendsBearerTokenAndCorrectBody_ReturnsVisitorId()
    {
        var (client, server) = await BuildAsync();
        using var _ = server;

        server.NextResponse = (200, """{"code":"SUCCESS","data":{"id":"visitor-123"},"msg":"success"}""");

        var start = new DateTimeOffset(2026, 8, 20, 1, 0, 0, TimeSpan.FromHours(2));
        var end = new DateTimeOffset(2026, 8, 25, 0, 0, 0, TimeSpan.FromHours(2));

        var visitorId = await client.CreateVisitorAsync(new CreateVisitorRequest
        {
            FirstName = "Alex",
            LastName = "Doe",
            StartTime = start,
            EndTime = end,
            Resources = new[] { new UnifiResourceRef { Id = "door-1", Name = "Front Door", Type = "door" } },
        });

        Assert.Equal("visitor-123", visitorId);

        var request = Assert.Single(server.Requests);
        Assert.Equal("Bearer test-token", request.Headers["Authorization"]);
        Assert.EndsWith("/api/v1/developer/visitors", request.Path);
        Assert.Contains("\"first_name\":\"Alex\"", request.Body);
        Assert.Contains($"\"start_time\":{start.ToUnixTimeSeconds()}", request.Body);
        Assert.Contains("\"type\":\"door\"", request.Body);
    }

    [Fact]
    public async Task AssignLicensePlatesAsync_SendsRawJsonArrayBody()
    {
        var (client, server) = await BuildAsync();
        using var _ = server;

        await client.AssignLicensePlatesAsync("visitor-1", new[] { "AB123C" });

        var request = Assert.Single(server.Requests);
        Assert.Equal("[\"AB123C\"]", request.Body);
        Assert.EndsWith("/visitors/visitor-1/license_plates", request.Path);
    }

    [Fact]
    public async Task GetDoorGroupTopologyAsync_FlattensGroupsAndDoors()
    {
        var (client, server) = await BuildAsync();
        using var _ = server;

        server.NextResponse = (200, """
            {
                "code": "SUCCESS",
                "data": [
                    {
                        "id": "group-1",
                        "name": "All Doors",
                        "type": "building",
                        "resource_topologies": [
                            {
                                "id": "floor-1",
                                "name": "Main Floor",
                                "type": "floor",
                                "resources": [
                                    { "id": "door-1", "name": "Front Door", "type": "door" }
                                ]
                            }
                        ]
                    }
                ],
                "msg": "success"
            }
            """);

        var resources = await client.GetDoorGroupTopologyAsync();

        Assert.Contains(resources, r => r.Id == "group-1" && r.Type == "door_group");
        Assert.Contains(resources, r => r.Id == "door-1" && r.Type == "door");
    }

    [Fact]
    public async Task CreateVisitorAsync_Throws_WhenApiReturnsNonSuccessCode()
    {
        var (client, server) = await BuildAsync();
        using var _ = server;

        server.NextResponse = (200, """{"code":"ERR_INVALID","data":null,"msg":"start_time is required"}""");

        var ex = await Assert.ThrowsAsync<UnifiAccessApiException>(() => client.CreateVisitorAsync(new CreateVisitorRequest
        {
            FirstName = "Alex",
            LastName = "Doe",
            StartTime = DateTimeOffset.UtcNow,
            EndTime = DateTimeOffset.UtcNow.AddDays(1),
        }));

        Assert.Equal("start_time is required", ex.Message);
        Assert.Equal("ERR_INVALID", ex.ApiCode);
    }
}
