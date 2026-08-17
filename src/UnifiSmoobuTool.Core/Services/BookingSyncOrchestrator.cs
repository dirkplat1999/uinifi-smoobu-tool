using Microsoft.Extensions.Logging;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Services;

/// <summary>
/// Drives the end-to-end automation state machine for every reservation in the sync window:
/// send the guest-info request 3 days before arrival, parse the guest's reply, provision (or
/// queue for manual review) the matching UniFi Access visitor, and revoke access on cancellation.
/// Every transition is persisted via <see cref="IReservationStateStore"/> before moving on, so a
/// restart mid-cycle never re-sends a message or re-provisions access.
/// </summary>
public sealed class BookingSyncOrchestrator
{
    private readonly ISmoobuClient _smoobu;
    private readonly IUnifiAccessClient _unifi;
    private readonly IReservationStateStore _stateStore;
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMessageTemplateStore _templateStore;
    private readonly IApartmentMappingStore _mappingStore;
    private readonly IWebhookConfigStore _webhookStore;
    private readonly ITestModeRuleStore _testModeStore;
    private readonly WebhookDispatcher _webhookDispatcher;
    private readonly IErrorNotifier _errorNotifier;
    private readonly IClock _clock;
    private readonly TimeZoneInfo _localTimeZone;
    private readonly ILogger<BookingSyncOrchestrator> _logger;

    public BookingSyncOrchestrator(
        ISmoobuClient smoobu,
        IUnifiAccessClient unifi,
        IReservationStateStore stateStore,
        IAppSettingsStore settingsStore,
        IMessageTemplateStore templateStore,
        IApartmentMappingStore mappingStore,
        IWebhookConfigStore webhookStore,
        ITestModeRuleStore testModeStore,
        WebhookDispatcher webhookDispatcher,
        IErrorNotifier errorNotifier,
        IClock clock,
        ILogger<BookingSyncOrchestrator> logger,
        TimeZoneInfo? localTimeZone = null)
    {
        _smoobu = smoobu ?? throw new ArgumentNullException(nameof(smoobu));
        _unifi = unifi ?? throw new ArgumentNullException(nameof(unifi));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _mappingStore = mappingStore ?? throw new ArgumentNullException(nameof(mappingStore));
        _webhookStore = webhookStore ?? throw new ArgumentNullException(nameof(webhookStore));
        _testModeStore = testModeStore ?? throw new ArgumentNullException(nameof(testModeStore));
        _webhookDispatcher = webhookDispatcher ?? throw new ArgumentNullException(nameof(webhookDispatcher));
        _errorNotifier = errorNotifier ?? throw new ArgumentNullException(nameof(errorNotifier));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localTimeZone = localTimeZone ?? TimeZoneInfo.Local;
    }

    /// <summary>Runs a full sync cycle across the rolling reservation window. Safe to call repeatedly
    /// from a timer; every step is idempotent against the persisted per-reservation state.</summary>
    public async Task RunOnceAsync(CancellationToken ct = default)
    {
        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(settings.SmoobuApiKey) ||
            string.IsNullOrWhiteSpace(settings.UnifiAccessHost) ||
            string.IsNullOrWhiteSpace(settings.UnifiAccessApiToken))
        {
            _logger.LogInformation("Sync skipped: Smoobu and/or UniFi Access are not fully configured yet.");
            return;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.ToOffset(_localTimeZone.GetUtcOffset(_clock.UtcNow.UtcDateTime)).DateTime);
        var windowStart = today.AddDays(-2);
        var windowEnd = today.AddDays(Math.Max(settings.MessageLeadDays, 0) + 30);

        IReadOnlyList<Reservation> reservations;
        try
        {
            reservations = await _smoobu.GetReservationsAsync(windowStart, windowEnd, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch reservations from Smoobu.");
            await _errorNotifier.NotifyErrorAsync("Smoobu", "Failed to fetch reservations.", ex, ct).ConfigureAwait(false);
            return;
        }

        var testModeRules = await _testModeStore.GetAllAsync(ct).ConfigureAwait(false);

        foreach (var reservation in reservations)
        {
            try
            {
                await ProcessReservationAsync(reservation, settings, testModeRules, today, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process reservation {ReservationId}.", reservation.Id);
                await _errorNotifier.NotifyErrorAsync(
                    "BookingSync", $"Failed to process reservation {reservation.Id}.", ex, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Applies a manually-corrected plate/PIN from the review queue and immediately provisions access.</summary>
    public async Task ApproveManualReviewAsync(long reservationId, string licensePlate, string pinCode, CancellationToken ct = default)
    {
        var reservation = await _smoobu.GetReservationAsync(reservationId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Reservation {reservationId} was not found in Smoobu.");

        var state = await _stateStore.GetAsync(reservationId, ct).ConfigureAwait(false)
            ?? new ReservationProcessingState { ReservationId = reservationId };

        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);

        state.ParsedLicensePlate = PlateNormalizer.Normalize(licensePlate, settings.LicensePlateCountryPrefixes);
        state.ParsedPinCode = pinCode.Trim();
        state.NeedsManualReview = false;
        state.GuestReplyReceivedAt ??= _clock.UtcNow;
        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);

        await ProvisionAccessAsync(reservation, state, settings, ct).ConfigureAwait(false);
    }

    private async Task ProcessReservationAsync(
        Reservation reservation,
        AppSettings settings,
        IReadOnlyList<TestModeRule> testModeRules,
        DateOnly today,
        CancellationToken ct)
    {
        if (!TestModeFilter.ShouldProcess(reservation, settings.TestModeEnabled, testModeRules))
        {
            return;
        }

        var state = await _stateStore.GetAsync(reservation.Id, ct).ConfigureAwait(false);
        bool isNewReservation = state is null;
        state ??= new ReservationProcessingState { ReservationId = reservation.Id };

        if (isNewReservation)
        {
            await FireWebhooksAsync(reservation, AutomationTrigger.ReservationCreated, settings, ct).ConfigureAwait(false);
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            await HandleCancellationAsync(reservation, state, settings, ct).ConfigureAwait(false);
            await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);
            return;
        }

        await MaybeSendRequestMessageAsync(reservation, state, settings, today, ct).ConfigureAwait(false);
        await MaybeParseGuestReplyAsync(reservation, state, settings, ct).ConfigureAwait(false);

        if (state.GuestReplyReceivedAt is not null && state.AccessCreatedAt is null && !state.NeedsManualReview)
        {
            await ProvisionAccessAsync(reservation, state, settings, ct).ConfigureAwait(false);
        }

        if (today == reservation.Arrival && state.ArrivalDayNotifiedAt is null)
        {
            await FireWebhooksAsync(reservation, AutomationTrigger.ArrivalDay, settings, ct).ConfigureAwait(false);
            state.ArrivalDayNotifiedAt = _clock.UtcNow;
        }

        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);
    }

    private async Task MaybeSendRequestMessageAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, DateOnly today, CancellationToken ct)
    {
        if (state.RequestMessageSentAt is not null)
        {
            return;
        }

        int daysUntilArrival = reservation.Arrival.DayNumber - today.DayNumber;
        if (daysUntilArrival < 0 || daysUntilArrival > settings.MessageLeadDays)
        {
            return;
        }

        var templates = await _templateStore.GetAllAsync(ct).ConfigureAwait(false);
        var template = TemplateRenderer.SelectTemplate(templates, reservation.GuestLanguage, settings.DefaultTemplateLanguage);

        var placeholders = BuildPlaceholders(reservation);
        var body = TemplateRenderer.Render(template.Body, placeholders);
        if (settings.TestModeEnabled)
        {
            body = "[TEST] " + body;
        }

        await _smoobu.SendMessageToGuestAsync(reservation.Id, body, ct).ConfigureAwait(false);
        state.RequestMessageSentAt = _clock.UtcNow;
        _logger.LogInformation("Sent guest-info request for reservation {ReservationId}.", reservation.Id);
    }

    private async Task MaybeParseGuestReplyAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, CancellationToken ct)
    {
        if (state.RequestMessageSentAt is null || state.GuestReplyReceivedAt is not null)
        {
            return;
        }

        var messages = await _smoobu.GetMessagesAsync(reservation.Id, ct).ConfigureAwait(false);
        var reply = messages
            .Where(m => m.Direction == MessageDirection.GuestToHost && m.SentAt >= state.RequestMessageSentAt)
            .OrderBy(m => m.SentAt)
            .FirstOrDefault();

        if (reply is null)
        {
            return;
        }

        var parsed = GuestReplyParser.Parse(reply.Text);
        state.GuestReplyReceivedAt = _clock.UtcNow;

        if (parsed.PinCode is null || parsed.RawLicensePlate is null)
        {
            state.NeedsManualReview = true;
            _logger.LogWarning("Could not confidently parse a reply for reservation {ReservationId}; flagged for manual review.", reservation.Id);
        }
        else
        {
            state.ParsedPinCode = parsed.PinCode;
            state.ParsedLicensePlate = PlateNormalizer.Normalize(parsed.RawLicensePlate, settings.LicensePlateCountryPrefixes);
            state.NeedsManualReview = !parsed.IsConfident || !settings.AutoApproveParsedReplies;
        }

        await FireWebhooksAsync(reservation, AutomationTrigger.GuestReplyReceived, settings, ct).ConfigureAwait(false);
    }

    private async Task ProvisionAccessAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, CancellationToken ct)
    {
        if (state.ParsedPinCode is null || state.ParsedLicensePlate is null)
        {
            return;
        }

        var mapping = await _mappingStore.GetAsync(reservation.ApartmentId, ct).ConfigureAwait(false);
        var resources = mapping?.UnifiResources ?? new List<UnifiResourceRef>();

        var (start, end) = AccessWindowCalculator.Calculate(reservation.Arrival, reservation.Departure, _localTimeZone);

        var firstName = settings.TestModeEnabled ? "[TEST] " + reservation.GuestFirstName : reservation.GuestFirstName;

        var visitorId = await _unifi.CreateVisitorAsync(new CreateVisitorRequest
        {
            FirstName = firstName,
            LastName = reservation.GuestLastName,
            StartTime = start,
            EndTime = end,
            Resources = resources,
        }, ct).ConfigureAwait(false);

        await _unifi.AssignPinCodeAsync(visitorId, state.ParsedPinCode, ct).ConfigureAwait(false);
        await _unifi.AssignLicensePlatesAsync(visitorId, new[] { state.ParsedLicensePlate }, ct).ConfigureAwait(false);

        state.UnifiVisitorId = visitorId;
        state.AccessCreatedAt = _clock.UtcNow;

        _logger.LogInformation(
            "Provisioned UniFi Access visitor {VisitorId} for reservation {ReservationId} ({Start} - {End}).",
            visitorId, reservation.Id, start, end);

        await FireWebhooksAsync(reservation, AutomationTrigger.AccessGranted, settings, ct).ConfigureAwait(false);
    }

    private async Task HandleCancellationAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, CancellationToken ct)
    {
        if (state.AccessCreatedAt is null || state.AccessRevokedAt is not null || state.UnifiVisitorId is null)
        {
            return;
        }

        await _unifi.DeleteVisitorAsync(state.UnifiVisitorId, force: false, ct).ConfigureAwait(false);
        state.AccessRevokedAt = _clock.UtcNow;

        _logger.LogInformation(
            "Revoked UniFi Access visitor {VisitorId} for cancelled reservation {ReservationId}.",
            state.UnifiVisitorId, reservation.Id);

        await FireWebhooksAsync(reservation, AutomationTrigger.AccessRevoked, settings, ct).ConfigureAwait(false);
    }

    private async Task FireWebhooksAsync(
        Reservation reservation, AutomationTrigger trigger, AppSettings settings, CancellationToken ct)
    {
        var webhooks = await _webhookStore.GetForApartmentAsync(reservation.ApartmentId, trigger, ct).ConfigureAwait(false);
        if (webhooks.Count == 0)
        {
            return;
        }

        var placeholders = BuildPlaceholders(reservation);
        await _webhookDispatcher.DispatchAllAsync(webhooks, placeholders, ct).ConfigureAwait(false);
    }

    private static Dictionary<string, string> BuildPlaceholders(Reservation reservation) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["guest_first_name"] = reservation.GuestFirstName,
        ["guest_last_name"] = reservation.GuestLastName,
        ["guest_full_name"] = reservation.GuestFullName,
        ["apartment_name"] = reservation.ApartmentName,
        ["arrival_date"] = reservation.Arrival.ToString("yyyy-MM-dd"),
        ["departure_date"] = reservation.Departure.ToString("yyyy-MM-dd"),
        ["reservation_id"] = reservation.Id.ToString(),
    };
}
