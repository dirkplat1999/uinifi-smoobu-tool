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
    private readonly IChannelMessagingSettingsStore _channelSettingsStore;
    private readonly IManualBookingStore _manualBookingStore;
    private readonly IGuestEmailSender _guestEmailSender;
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
        IChannelMessagingSettingsStore channelSettingsStore,
        IManualBookingStore manualBookingStore,
        IGuestEmailSender guestEmailSender,
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
        _channelSettingsStore = channelSettingsStore ?? throw new ArgumentNullException(nameof(channelSettingsStore));
        _manualBookingStore = manualBookingStore ?? throw new ArgumentNullException(nameof(manualBookingStore));
        _guestEmailSender = guestEmailSender ?? throw new ArgumentNullException(nameof(guestEmailSender));
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

        if (string.IsNullOrWhiteSpace(settings.UnifiAccessHost) || string.IsNullOrWhiteSpace(settings.UnifiAccessApiToken))
        {
            _logger.LogInformation("Sync skipped: UniFi Access is not configured yet.");
            return;
        }

        var today = DateOnly.FromDateTime(_clock.UtcNow.ToOffset(_localTimeZone.GetUtcOffset(_clock.UtcNow.UtcDateTime)).DateTime);
        var windowStart = today.AddDays(-2);
        var windowEnd = today.AddDays(Math.Max(settings.MessageLeadDays, 0) + 30);

        var reservations = new List<Reservation>();

        if (string.IsNullOrWhiteSpace(settings.SmoobuApiKey))
        {
            _logger.LogInformation("Smoobu isn't configured yet - only manual bookings will be processed this cycle.");
        }
        else
        {
            try
            {
                reservations.AddRange(await _smoobu.GetReservationsAsync(windowStart, windowEnd, ct).ConfigureAwait(false));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch reservations from Smoobu.");
                await _errorNotifier.NotifyErrorAsync("Smoobu", "Failed to fetch reservations.", ex, ct).ConfigureAwait(false);
            }
        }

        try
        {
            var manualBookings = await _manualBookingStore.GetAllAsync(ct).ConfigureAwait(false);
            reservations.AddRange(manualBookings.Select(ToReservation));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load manual bookings.");
            await _errorNotifier.NotifyErrorAsync("ManualBookings", "Failed to load manual bookings.", ex, ct).ConfigureAwait(false);
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
        var reservation = await GetReservationAsync(reservationId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Reservation {reservationId} was not found.");

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

    /// <summary>Checked from the Dashboard to force-send guest messages for one reservation even
    /// though its booking channel currently has messaging disabled.</summary>
    public async Task SetMessagingOverrideAsync(long reservationId, bool enabled, CancellationToken ct = default)
    {
        var state = await _stateStore.GetAsync(reservationId, ct).ConfigureAwait(false)
            ?? new ReservationProcessingState { ReservationId = reservationId };

        state.MessagingOverrideEnabled = enabled;
        await _stateStore.SaveAsync(state, ct).ConfigureAwait(false);
    }

    /// <summary>Looks up a reservation by id regardless of source - Smoobu ids go to Smoobu, ids in
    /// the manual-booking range (see <see cref="ManualBookingReservationId"/>) go to the manual
    /// booking store.</summary>
    private async Task<Reservation?> GetReservationAsync(long reservationId, CancellationToken ct)
    {
        if (ManualBookingReservationId.TryGetManualBookingId(reservationId, out var manualBookingId))
        {
            var booking = await _manualBookingStore.GetAsync(manualBookingId, ct).ConfigureAwait(false);
            return booking is null ? null : ToReservation(booking);
        }

        return await _smoobu.GetReservationAsync(reservationId, ct).ConfigureAwait(false);
    }

    private static Reservation ToReservation(ManualBooking booking) => new()
    {
        Id = ManualBookingReservationId.ToReservationId(booking.Id),
        ApartmentId = booking.ApartmentId,
        ApartmentName = booking.ApartmentName,
        GuestFirstName = booking.GuestFirstName,
        GuestLastName = booking.GuestLastName,
        GuestEmail = booking.GuestEmail,
        GuestLanguage = booking.GuestLanguage,
        Channel = "Manual",
        Arrival = booking.Arrival,
        Departure = booking.Departure,
        Status = booking.Cancelled ? ReservationStatus.Cancelled : ReservationStatus.Confirmed,
        Source = ReservationSource.Manual,
    };

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

        if (!string.IsNullOrWhiteSpace(reservation.Channel))
        {
            await _channelSettingsStore.EnsureRegisteredAsync(reservation.Channel, ct).ConfigureAwait(false);
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

        if (reservation.Source == ReservationSource.Manual)
        {
            // No inbound channel exists for a manually-entered booking's reply - once the request
            // has gone out, it just waits in the manual-review queue for the host to fill in what
            // the guest replied with by email.
            if (state.RequestMessageSentAt is not null && state.AccessCreatedAt is null)
            {
                state.NeedsManualReview = true;
            }
        }
        else
        {
            await MaybeParseGuestReplyAsync(reservation, state, settings, ct).ConfigureAwait(false);
        }

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
        if (state.RequestMessageSentAt is not null || !await ShouldSendGuestMessagesAsync(reservation, state, settings, ct).ConfigureAwait(false))
        {
            return;
        }

        int daysUntilArrival = reservation.Arrival.DayNumber - today.DayNumber;
        if (daysUntilArrival < 0 || daysUntilArrival > settings.MessageLeadDays)
        {
            return;
        }

        await SendGuestMessageAsync(reservation, MessageTemplateKind.Request, settings, ct).ConfigureAwait(false);
        state.RequestMessageSentAt = _clock.UtcNow;
        _logger.LogInformation("Sent guest-info request for reservation {ReservationId}.", reservation.Id);
    }

    /// <summary>Parses the guest's reply once a request has been sent. When the reply can't be
    /// confidently read, sends one automatic clarification request and allows a single follow-up
    /// reply to be re-checked; when it's read clearly, sends a confirmation regardless of whether
    /// <see cref="AppSettings.AutoApproveParsedReplies"/> still requires manual sign-off - that
    /// setting controls internal review, not whether the guest gets a "thanks, got it".</summary>
    private async Task MaybeParseGuestReplyAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, CancellationToken ct)
    {
        if (state.RequestMessageSentAt is null)
        {
            return;
        }

        if (state.GuestReplyReceivedAt is not null && !state.NeedsManualReview)
        {
            return;
        }

        if (state.GuestReplyReceivedAt is not null && state.NeedsManualReview && state.ClarificationRequestedAt is null)
        {
            // Already flagged for manual review and no clarification was sent (e.g. guest messaging
            // was disabled at the time) - nothing more to do automatically; a human resolves it.
            return;
        }

        var since = state.ClarificationRequestedAt ?? state.RequestMessageSentAt.Value;

        var messages = await _smoobu.GetMessagesAsync(reservation.Id, ct).ConfigureAwait(false);
        var reply = messages
            .Where(m => m.Direction == MessageDirection.GuestToHost && m.SentAt >= since)
            .OrderBy(m => m.SentAt)
            .FirstOrDefault();

        if (reply is null)
        {
            return;
        }

        var parsed = GuestReplyParser.Parse(reply.Text);
        state.GuestReplyReceivedAt = _clock.UtcNow;

        bool parseFoundBoth = parsed.PinCode is not null && parsed.RawLicensePlate is not null;
        bool parseIsClear = parseFoundBoth && parsed.IsConfident;

        if (parseFoundBoth)
        {
            state.ParsedPinCode = parsed.PinCode;
            state.ParsedLicensePlate = PlateNormalizer.Normalize(parsed.RawLicensePlate!, settings.LicensePlateCountryPrefixes);
        }
        else
        {
            _logger.LogWarning("Could not confidently parse a reply for reservation {ReservationId}; flagged for manual review.", reservation.Id);
        }

        state.NeedsManualReview = !parseIsClear || !settings.AutoApproveParsedReplies;

        var shouldSendMessages = await ShouldSendGuestMessagesAsync(reservation, state, settings, ct).ConfigureAwait(false);
        if (parseIsClear)
        {
            if (shouldSendMessages)
            {
                await SendGuestMessageAsync(reservation, MessageTemplateKind.Confirmation, settings, ct).ConfigureAwait(false);
                state.ConfirmationSentAt = _clock.UtcNow;
            }
        }
        else if (shouldSendMessages && state.ClarificationRequestedAt is null)
        {
            await SendGuestMessageAsync(reservation, MessageTemplateKind.Clarification, settings, ct).ConfigureAwait(false);
            state.ClarificationRequestedAt = _clock.UtcNow;
        }

        await FireWebhooksAsync(reservation, AutomationTrigger.GuestReplyReceived, settings, ct).ConfigureAwait(false);
    }

    /// <summary>Gate for every guest-facing message: the global master switch, then (if the
    /// reservation has a known booking channel) that channel's on/off setting, with a per-reservation
    /// override that lets one specific guest still be messaged on an otherwise-disabled channel.</summary>
    private async Task<bool> ShouldSendGuestMessagesAsync(
        Reservation reservation, ReservationProcessingState state, AppSettings settings, CancellationToken ct)
    {
        if (!settings.GuestMessagingEnabled)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(reservation.Channel))
        {
            return true;
        }

        var channelSetting = await _channelSettingsStore.GetAsync(reservation.Channel, ct).ConfigureAwait(false);
        return channelSetting is null || channelSetting.Enabled || state.MessagingOverrideEnabled;
    }

    private async Task SendGuestMessageAsync(
        Reservation reservation, MessageTemplateKind kind, AppSettings settings, CancellationToken ct)
    {
        var templates = await _templateStore.GetAllAsync(ct).ConfigureAwait(false);
        var template = TemplateRenderer.SelectTemplate(templates, kind, reservation.GuestLanguage, settings.DefaultTemplateLanguage);

        var placeholders = BuildPlaceholders(reservation);
        var body = TemplateRenderer.Render(template.Body, placeholders);
        if (settings.TestModeEnabled)
        {
            body = "[TEST] " + body;
        }

        if (reservation.Source == ReservationSource.Manual)
        {
            if (string.IsNullOrWhiteSpace(reservation.GuestEmail) || settings.Smtp is null)
            {
                _logger.LogWarning(
                    "Couldn't email reservation {ReservationId}: a guest email and SMTP settings are both required for manual bookings.",
                    reservation.Id);
                return;
            }

            var subjectTemplate = template.Subject
                ?? DefaultMessageTemplates.TryGetSubject(reservation.GuestLanguage ?? settings.DefaultTemplateLanguage, kind)
                ?? "UniFi Smoobu Tool";
            var subject = TemplateRenderer.Render(subjectTemplate, placeholders);
            if (settings.TestModeEnabled)
            {
                subject = "[TEST] " + subject;
            }

            await _guestEmailSender.SendAsync(settings.Smtp, reservation.GuestEmail, subject, body, ct).ConfigureAwait(false);
        }
        else
        {
            await _smoobu.SendMessageToGuestAsync(reservation.Id, body, ct).ConfigureAwait(false);
        }
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
        ["arrival_date"] = reservation.Arrival.ToString("dd-MM-yyyy"),
        ["departure_date"] = reservation.Departure.ToString("dd-MM-yyyy"),
        ["reservation_id"] = reservation.Id.ToString(),
    };
}
