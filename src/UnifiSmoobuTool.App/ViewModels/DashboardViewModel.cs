using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class ReservationRowViewModel : ObservableObject
{
    public required Reservation Reservation { get; init; }
    public required ReservationProcessingState? State { get; init; }

    /// <summary>True when this reservation's booking channel currently has guest messaging turned
    /// off - controls whether the "Send messages anyway" override checkbox is shown at all.</summary>
    public required bool IsChannelMessagingDisabled { get; init; }

    [ObservableProperty]
    private bool _sendMessagesAnyway;

    public string GuestName => Reservation.GuestFullName;
    public string ApartmentName => Reservation.ApartmentName;
    public string Channel => string.IsNullOrWhiteSpace(Reservation.Channel) ? "-" : Reservation.Channel;
    public string Language => string.IsNullOrWhiteSpace(Reservation.GuestLanguage) ? "-" : Reservation.GuestLanguage.ToUpperInvariant();
    public string Arrival => Reservation.Arrival.ToString("dd-MM-yyyy");
    public string Departure => Reservation.Departure.ToString("dd-MM-yyyy");

    public string Status
    {
        get
        {
            if (Reservation.Status == ReservationStatus.Cancelled) return "Cancelled";
            if (State is null) return "Pending";
            if (State.AccessRevokedAt is not null) return "Access revoked";
            if (State.AccessCreatedAt is not null) return "Access granted";
            if (State.NeedsManualReview && State.ClarificationRequestedAt is not null && State.GuestReplyReceivedAt < State.ClarificationRequestedAt)
                return "Awaiting guest clarification";
            if (State.NeedsManualReview) return "Needs review";
            if (State.GuestReplyReceivedAt is not null) return "Reply received";
            if (State.RequestMessageSentAt is not null) return "Message sent";
            return "Waiting";
        }
    }
}

public sealed partial class PendingReviewRowViewModel : ObservableObject
{
    public required long ReservationId { get; init; }
    public required string GuestName { get; init; }

    [ObservableProperty]
    private string _licensePlate = "";

    [ObservableProperty]
    private string _pinCode = "";
}

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly ISmoobuClient _smoobu;
    private readonly IReservationStateStore _stateStore;
    private readonly IChannelMessagingSettingsStore _channelSettingsStore;
    private readonly BookingSyncOrchestrator _orchestrator;
    private readonly IClock _clock;

    [ObservableProperty]
    private string _statusMessage = "Click Refresh to load upcoming reservations.";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ReservationRowViewModel> UpcomingReservations { get; } = new();
    public ObservableCollection<PendingReviewRowViewModel> PendingReview { get; } = new();

    public DashboardViewModel(
        ISmoobuClient smoobu,
        IReservationStateStore stateStore,
        IChannelMessagingSettingsStore channelSettingsStore,
        BookingSyncOrchestrator orchestrator,
        IClock clock)
    {
        _smoobu = smoobu;
        _stateStore = stateStore;
        _channelSettingsStore = channelSettingsStore;
        _orchestrator = orchestrator;
        _clock = clock;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading...";
        try
        {
            var today = DateOnly.FromDateTime(_clock.UtcNow.LocalDateTime);
            var reservations = await _smoobu.GetReservationsAsync(today.AddDays(-2), today.AddDays(45));
            var channelSettings = await _channelSettingsStore.GetAllAsync();
            var disabledChannels = channelSettings.Where(c => !c.Enabled).Select(c => c.ChannelName).ToHashSet(StringComparer.OrdinalIgnoreCase);

            UpcomingReservations.Clear();
            PendingReview.Clear();

            foreach (var reservation in reservations.OrderBy(r => r.Arrival))
            {
                var state = await _stateStore.GetAsync(reservation.Id);
                var isChannelDisabled = !string.IsNullOrWhiteSpace(reservation.Channel) && disabledChannels.Contains(reservation.Channel);
                UpcomingReservations.Add(new ReservationRowViewModel
                {
                    Reservation = reservation,
                    State = state,
                    IsChannelMessagingDisabled = isChannelDisabled,
                    SendMessagesAnyway = state?.MessagingOverrideEnabled ?? false,
                });

                if (state is { NeedsManualReview: true })
                {
                    PendingReview.Add(new PendingReviewRowViewModel
                    {
                        ReservationId = reservation.Id,
                        GuestName = reservation.GuestFullName,
                        LicensePlate = state.ParsedLicensePlate ?? "",
                        PinCode = state.ParsedPinCode ?? "",
                    });
                }
            }

            StatusMessage = $"Loaded {reservations.Count} reservation(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load reservations: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SyncNowAsync()
    {
        IsBusy = true;
        StatusMessage = "Running sync...";
        try
        {
            await _orchestrator.RunOnceAsync();
            StatusMessage = "Sync complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    [RelayCommand]
    private async Task ApproveAsync(PendingReviewRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _orchestrator.ApproveManualReviewAsync(row.ReservationId, row.LicensePlate, row.PinCode);
            StatusMessage = $"Access provisioned for reservation {row.ReservationId}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't approve: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            await RefreshAsync();
        }
    }

    /// <summary>Force-sends (or stops forcing) guest messages for one reservation whose booking
    /// channel currently has messaging turned off, via the Dashboard checkbox.</summary>
    [RelayCommand]
    private async Task ToggleMessagingOverrideAsync(ReservationRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        try
        {
            await _orchestrator.SetMessagingOverrideAsync(row.Reservation.Id, row.SendMessagesAnyway);
            StatusMessage = row.SendMessagesAnyway
                ? $"Will message {row.GuestName} anyway, even though the {row.Channel} channel is off."
                : $"No longer overriding the channel setting for {row.GuestName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't update the override: {ex.Message}";
        }
    }
}
