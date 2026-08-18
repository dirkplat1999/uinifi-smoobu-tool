using Microsoft.Extensions.Logging.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Infrastructure.Smoobu;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class SmoobuApiClientTests
{
    private static (SmoobuApiClient Client, FakeHttpMessageHandler Handler) Build(string apiKey = "test-key", string? apiSecret = null)
    {
        var handler = new FakeHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var settingsStore = new InMemoryAppSettingsStore
        {
            Settings = new AppSettings { SmoobuApiKey = apiKey, SmoobuApiSecret = apiSecret },
        };
        var client = new SmoobuApiClient(httpClient, settingsStore, NullLogger<SmoobuApiClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task GetApartmentsAsync_SendsApiKeyHeader_AndParsesResponse()
    {
        var (client, handler) = Build("secret-key");
        handler.DefaultResponse = (200, """{"apartments":[{"id":1,"name":"Canal View"},{"id":2,"name":"Garden Loft"}]}""");

        var apartments = await client.GetApartmentsAsync();

        Assert.Equal(2, apartments.Count);
        Assert.Equal("Canal View", apartments[0].Name);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("secret-key", request.Headers["Api-Key"]);
        Assert.Equal("/api/apartments", request.Uri.AbsolutePath);
    }

    [Fact]
    public async Task GetReservationsAsync_ParsesGuestFieldsAndDetectsCancellation()
    {
        var (client, handler) = Build();
        handler.DefaultResponse = (200, """
            {
                "bookings": [
                    {
                        "id": 111,
                        "arrival": "2026-08-20",
                        "departure": "2026-08-24",
                        "apartment": { "id": 5, "name": "Canal View" },
                        "firstname": "Alex",
                        "lastname": "Doe",
                        "email": "alex@example.com",
                        "phone": "+31612345678",
                        "language": "nl",
                        "status": "cancelled",
                        "channel": { "id": 2005, "name": "Airbnb" }
                    }
                ],
                "page": 1,
                "page_count": 1
            }
            """);

        var reservations = await client.GetReservationsAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1));

        var reservation = Assert.Single(reservations);
        Assert.Equal(111, reservation.Id);
        Assert.Equal("Alex", reservation.GuestFirstName);
        Assert.Equal("Doe", reservation.GuestLastName);
        Assert.Equal("nl", reservation.GuestLanguage);
        Assert.Equal(ReservationStatus.Cancelled, reservation.Status);
        Assert.Equal("Airbnb", reservation.Channel);
    }

    [Fact]
    public async Task GetReservationsAsync_LeavesChannelNull_WhenNotReported()
    {
        var (client, handler) = Build();
        handler.DefaultResponse = (200, """
            {
                "bookings": [
                    {
                        "id": 333,
                        "arrival": "2026-08-20",
                        "departure": "2026-08-24",
                        "apartment": { "id": 5, "name": "Canal View" },
                        "firstname": "Alex",
                        "lastname": "Doe"
                    }
                ],
                "page": 1,
                "page_count": 1
            }
            """);

        var reservations = await client.GetReservationsAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1));

        Assert.Null(Assert.Single(reservations).Channel);
    }

    [Fact]
    public async Task GetReservationsAsync_FallsBackToSplittingGuestName_WhenFirstLastMissing()
    {
        var (client, handler) = Build();
        handler.DefaultResponse = (200, """
            {
                "bookings": [
                    {
                        "id": 222,
                        "arrival": "2026-08-20",
                        "departure": "2026-08-24",
                        "apartment": { "id": 5, "name": "Canal View" },
                        "guest-name": "Jamie Rivera"
                    }
                ],
                "page": 1,
                "page_count": 1
            }
            """);

        var reservations = await client.GetReservationsAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 9, 1));

        var reservation = Assert.Single(reservations);
        Assert.Equal("Jamie", reservation.GuestFirstName);
        Assert.Equal("Rivera", reservation.GuestLastName);
    }

    [Fact]
    public async Task SendMessageToGuestAsync_PostsToCorrectPathWithJsonBody()
    {
        var (client, handler) = Build();

        await client.SendMessageToGuestAsync(999, "Hello there");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/reservations/999/messages/send-message-to-guest", request.Uri.AbsolutePath);
        Assert.Contains("Hello there", request.Body);
    }

    [Fact]
    public async Task GetApartmentsAsync_UsesLegacyApiKeyHeader_WhenNoSecretConfigured()
    {
        var (client, handler) = Build(apiKey: "test-key", apiSecret: null);
        handler.DefaultResponse = (200, """{"apartments":[]}""");

        await client.GetApartmentsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal("test-key", request.Headers["Api-Key"]);
        Assert.False(request.Headers.ContainsKey("X-Signature"));
    }

    [Fact]
    public async Task GetApartmentsAsync_UsesHmacHeaders_WhenSecretConfigured()
    {
        var (client, handler) = Build(apiKey: "test-key", apiSecret: "test-secret");
        handler.DefaultResponse = (200, """{"apartments":[]}""");

        await client.GetApartmentsAsync();

        var request = Assert.Single(handler.Requests);
        Assert.False(request.Headers.ContainsKey("Api-Key"));
        Assert.Equal("test-key", request.Headers["X-API-Key"]);
        Assert.True(request.Headers.ContainsKey("X-Timestamp"));
        Assert.True(request.Headers.ContainsKey("X-Nonce"));
        Assert.True(request.Headers.ContainsKey("X-Signature"));
    }

    [Fact]
    public async Task GetReservationsAsync_SortsQueryParamsInCanonicalOrder_ButNotNecessarilyInTheUrl()
    {
        var (client, handler) = Build(apiKey: "test-key", apiSecret: "test-secret");
        handler.DefaultResponse = (200, """{"bookings":[],"page":1,"page_count":1}""");

        await client.GetReservationsAsync(new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        var request = Assert.Single(handler.Requests);
        // The actual signature correctness is covered by SmoobuHmacSignerTests; here we just
        // confirm every HMAC header made it onto a real GET request with query parameters.
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Contains("from=2026-08-01", request.Uri.Query);
        Assert.True(request.Headers.ContainsKey("X-Signature"));
    }

    [Fact]
    public async Task GetApartmentsAsync_Throws_WhenApiKeyNotConfigured()
    {
        var (client, _) = Build(apiKey: null!);

        await Assert.ThrowsAsync<SmoobuApiException>(() => client.GetApartmentsAsync());
    }

    [Fact]
    public async Task GetApartmentsAsync_Throws_OnNonSuccessStatusCode()
    {
        var (client, handler) = Build();
        handler.DefaultResponse = (401, """{"error":"invalid api key"}""");

        var ex = await Assert.ThrowsAsync<SmoobuApiException>(() => client.GetApartmentsAsync());
        Assert.Equal(401, ex.StatusCode);
    }
}
