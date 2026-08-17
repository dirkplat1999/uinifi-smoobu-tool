using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.UnifiAccess;

/// <summary>
/// Typed client for the local UniFi Access controller's Open API
/// (https://&lt;controller&gt;:12445/api/v1/developer/...), bearer-token authenticated. The
/// controller normally presents a self-signed certificate (the vendor's own API examples all pass
/// --insecure), so certificate trust is configurable rather than hard-coded. Host/token/trust are
/// re-read from settings on every call, and the underlying HttpClient is rebuilt automatically
/// whenever the host or trust setting changes so Settings updates apply without an app restart.
/// </summary>
public sealed class UnifiAccessApiClient : IUnifiAccessClient, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAppSettingsStore _settingsStore;
    private readonly ILogger<UnifiAccessApiClient> _logger;

    private readonly SemaphoreSlim _clientLock = new(1, 1);
    private HttpClient? _httpClient;
    private string? _cachedHost;
    private bool _cachedTrustAny;

    public UnifiAccessApiClient(IAppSettingsStore settingsStore, ILogger<UnifiAccessApiClient> logger)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<UnifiResourceRef>> GetDoorGroupTopologyAsync(CancellationToken ct = default)
    {
        var data = await GetAsync<List<UnifiDoorGroupTopologyDto>>("door_groups/topology", ct).ConfigureAwait(false);
        var groups = data ?? new List<UnifiDoorGroupTopologyDto>();

        var results = new List<UnifiResourceRef>();
        foreach (var group in groups)
        {
            results.Add(new UnifiResourceRef { Id = group.Id, Name = group.Name, Type = "door_group" });

            foreach (var floor in group.ResourceTopologies ?? Enumerable.Empty<UnifiResourceTopologyDto>())
            {
                foreach (var door in floor.Resources ?? Enumerable.Empty<UnifiDoorResourceDto>())
                {
                    results.Add(new UnifiResourceRef { Id = door.Id, Name = door.Name, Type = door.Type ?? "door" });
                }
            }
        }

        return results
            .GroupBy(r => (r.Id, r.Type))
            .Select(g => g.First())
            .ToList();
    }

    public async Task<string> CreateVisitorAsync(CreateVisitorRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = new UnifiCreateVisitorRequestDto
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            StartTime = request.StartTime.ToUnixTimeSeconds(),
            EndTime = request.EndTime.ToUnixTimeSeconds(),
            VisitReason = request.VisitReason,
            Resources = request.Resources
                .Select(r => new UnifiResourceRequestDto { Id = r.Id, Type = r.Type })
                .ToList(),
        };

        var data = await PostAsync<UnifiCreateVisitorRequestDto, UnifiVisitorDataDto>("visitors", dto, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(data?.Id))
        {
            throw new UnifiAccessApiException("UniFi Access did not return a visitor id after creation.");
        }

        return data.Id;
    }

    public async Task UpdateVisitorAsync(string visitorId, UpdateVisitorRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var dto = new UnifiUpdateVisitorRequestDto
        {
            StartTime = request.StartTime?.ToUnixTimeSeconds(),
            EndTime = request.EndTime?.ToUnixTimeSeconds(),
        };

        await SendAsync(HttpMethod.Put, $"visitors/{Uri.EscapeDataString(visitorId)}", dto, ct).ConfigureAwait(false);
    }

    public async Task DeleteVisitorAsync(string visitorId, bool force, CancellationToken ct = default)
    {
        var path = $"visitors/{Uri.EscapeDataString(visitorId)}" + (force ? "?is_force=true" : "");
        await SendAsync<object?>(HttpMethod.Delete, path, null, ct).ConfigureAwait(false);
    }

    public async Task AssignPinCodeAsync(string visitorId, string pinCode, CancellationToken ct = default)
    {
        var dto = new UnifiPinCodeRequestDto { PinCode = pinCode };
        await SendAsync(HttpMethod.Put, $"visitors/{Uri.EscapeDataString(visitorId)}/pin_codes", dto, ct).ConfigureAwait(false);
    }

    public async Task AssignLicensePlatesAsync(string visitorId, IReadOnlyList<string> plates, CancellationToken ct = default)
    {
        await SendAsync(HttpMethod.Put, $"visitors/{Uri.EscapeDataString(visitorId)}/license_plates", plates, ct).ConfigureAwait(false);
    }

    private async Task<TResponse?> GetAsync<TResponse>(string path, CancellationToken ct)
        => await SendAsync<object?, TResponse>(HttpMethod.Get, path, null, ct).ConfigureAwait(false);

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
        => await SendAsync<TRequest, TResponse>(HttpMethod.Post, path, body, ct).ConfigureAwait(false);

    private Task SendAsync<TRequest>(HttpMethod method, string path, TRequest? body, CancellationToken ct)
        => SendAsync<TRequest, object?>(method, path, body, ct);

    private async Task<TResponse?> SendAsync<TRequest, TResponse>(HttpMethod method, string path, TRequest? body, CancellationToken ct)
    {
        var (client, token) = await EnsureClientAsync(ct).ConfigureAwait(false);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        var responseBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("UniFi Access API call to {Path} failed with {StatusCode}: {Body}", path, (int)response.StatusCode, responseBody);
            throw new UnifiAccessApiException(
                $"UniFi Access API returned {(int)response.StatusCode} {response.ReasonPhrase}.", (int)response.StatusCode);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return default;
        }

        var envelope = JsonSerializer.Deserialize<UnifiApiEnvelope<TResponse>>(responseBody, JsonOptions);
        if (envelope is not null && !string.Equals(envelope.Code, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            throw new UnifiAccessApiException(envelope.Msg ?? "UniFi Access API call failed.", (int)response.StatusCode, envelope.Code);
        }

        return envelope is null ? default : envelope.Data;
    }

    private async Task<(HttpClient Client, string Token)> EnsureClientAsync(CancellationToken ct)
    {
        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(settings.UnifiAccessHost) || string.IsNullOrWhiteSpace(settings.UnifiAccessApiToken))
        {
            throw new UnifiAccessApiException("UniFi Access host/API token are not configured.");
        }

        var host = settings.UnifiAccessHost.TrimEnd('/');

        await _clientLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_httpClient is null || _cachedHost != host || _cachedTrustAny != settings.UnifiAccessTrustAnySslCert)
            {
                _httpClient?.Dispose();

                var handler = new HttpClientHandler();
                if (settings.UnifiAccessTrustAnySslCert)
                {
                    handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
                }

                _httpClient = new HttpClient(handler)
                {
                    BaseAddress = new Uri(host + "/api/v1/developer/"),
                    Timeout = TimeSpan.FromSeconds(30),
                };
                _cachedHost = host;
                _cachedTrustAny = settings.UnifiAccessTrustAnySslCert;
            }

            return (_httpClient, settings.UnifiAccessApiToken);
        }
        finally
        {
            _clientLock.Release();
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _clientLock.Dispose();
    }
}
