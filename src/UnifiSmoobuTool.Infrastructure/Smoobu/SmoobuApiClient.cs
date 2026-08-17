using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Smoobu;

/// <summary>
/// Typed client for the Smoobu REST API (https://login.smoobu.com), using the legacy single
/// "Api-Key" header. The API key is re-read from settings on every call so a key change in the
/// Settings screen takes effect without restarting the background sync loop.
/// </summary>
public sealed class SmoobuApiClient : ISmoobuClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<SmoobuApiClient> _logger;

    public SmoobuApiClient(HttpClient httpClient, IAppSettingsStore settingsStore, ILogger<SmoobuApiClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _httpClient.BaseAddress ??= new Uri("https://login.smoobu.com");
    }

    public async Task<IReadOnlyList<Apartment>> GetApartmentsAsync(CancellationToken ct = default)
    {
        var dto = await GetAsync<SmoobuApartmentListDto>("/api/apartments", ct).ConfigureAwait(false);
        return (dto?.Apartments ?? new List<SmoobuApartmentDto>())
            .Select(a => new Apartment { Id = a.Id, Name = string.IsNullOrWhiteSpace(a.Name) ? $"Apartment {a.Id}" : a.Name })
            .ToList();
    }

    public async Task<IReadOnlyList<Reservation>> GetReservationsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var results = new List<Reservation>();
        int page = 1;
        int pageCount = 1;

        do
        {
            var path = $"/api/reservations?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&showCancellation=1&pageSize=100&page={page}";
            var dto = await GetAsync<SmoobuReservationListDto>(path, ct).ConfigureAwait(false);
            if (dto?.Bookings is not null)
            {
                results.AddRange(dto.Bookings.Select(ToReservation));
            }

            pageCount = dto?.PageCount ?? 1;
            page++;
        } while (page <= pageCount && pageCount > 1);

        // Defensive client-side filter in case the server's date filter semantics differ from a
        // simple stay-overlap check.
        return results.Where(r => r.Arrival <= to && r.Departure >= from).ToList();
    }

    public async Task<Reservation?> GetReservationAsync(long reservationId, CancellationToken ct = default)
    {
        var dto = await GetAsync<SmoobuReservationDto>($"/api/reservations/{reservationId}", ct).ConfigureAwait(false);
        return dto is null ? null : ToReservation(dto);
    }

    public async Task<IReadOnlyList<GuestMessage>> GetMessagesAsync(long reservationId, CancellationToken ct = default)
    {
        var dto = await GetAsync<SmoobuMessageListDto>($"/api/reservations/{reservationId}/messages", ct).ConfigureAwait(false);
        return (dto?.Messages ?? new List<SmoobuMessageDto>())
            .Select(m => new GuestMessage
            {
                ReservationId = reservationId,
                Text = m.Message ?? m.Text ?? string.Empty,
                SentAt = m.CreatedAt ?? DateTimeOffset.UtcNow,
                Direction = (m.FromGuest ?? string.Equals(m.Type, "guest", StringComparison.OrdinalIgnoreCase))
                    ? MessageDirection.GuestToHost
                    : MessageDirection.HostToGuest,
            })
            .ToList();
    }

    public async Task SendMessageToGuestAsync(long reservationId, string message, CancellationToken ct = default)
    {
        var body = JsonSerializer.Serialize(new SmoobuSendMessageRequestDto { Message = message }, JsonOptions);
        using var request = await CreateRequestAsync(
            HttpMethod.Post, $"/api/reservations/{reservationId}/messages/send-message-to-guest", ct).ConfigureAwait(false);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);
    }

    private static Reservation ToReservation(SmoobuReservationDto dto)
    {
        var firstName = dto.FirstName;
        var lastName = dto.LastName;
        if (string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName) && !string.IsNullOrWhiteSpace(dto.GuestName))
        {
            var parts = dto.GuestName!.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            firstName = parts.Length > 0 ? parts[0] : dto.GuestName;
            lastName = parts.Length > 1 ? parts[1] : string.Empty;
        }

        bool isCancelled = dto.IsCancelled == true ||
            (dto.Status?.Contains("cancel", StringComparison.OrdinalIgnoreCase) ?? false);

        return new Reservation
        {
            Id = dto.Id,
            ApartmentId = dto.Apartment?.Id ?? 0,
            ApartmentName = dto.Apartment?.Name ?? "Unknown apartment",
            GuestFirstName = firstName ?? string.Empty,
            GuestLastName = lastName ?? string.Empty,
            GuestEmail = dto.Email,
            GuestPhone = dto.Phone,
            GuestLanguage = dto.Language,
            Arrival = dto.Arrival,
            Departure = dto.Departure,
            Status = isCancelled ? ReservationStatus.Cancelled : ReservationStatus.Confirmed,
        };
    }

    private async Task<T?> GetAsync<T>(string path, CancellationToken ct)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, path, ct).ConfigureAwait(false);
        using var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        await EnsureSuccessAsync(response, ct).ConfigureAwait(false);

        await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string path, CancellationToken ct)
    {
        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(settings.SmoobuApiKey))
        {
            throw new SmoobuApiException("No Smoobu API key is configured.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("Api-Key", settings.SmoobuApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        _logger.LogError("Smoobu API call to {Uri} failed with {StatusCode}: {Body}", response.RequestMessage?.RequestUri, (int)response.StatusCode, body);
        throw new SmoobuApiException(
            $"Smoobu API returned {(int)response.StatusCode} {response.ReasonPhrase}.", (int)response.StatusCode);
    }
}
