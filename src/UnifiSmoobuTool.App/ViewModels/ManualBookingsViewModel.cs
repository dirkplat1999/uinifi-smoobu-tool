using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class ManualBookingRowViewModel : ObservableObject
{
    public required long Id { get; init; }

    [ObservableProperty]
    private int _apartmentId;

    [ObservableProperty]
    private string _apartmentName = "";

    [ObservableProperty]
    private string _guestFirstName = "";

    [ObservableProperty]
    private string _guestLastName = "";

    [ObservableProperty]
    private string _guestEmail = "";

    [ObservableProperty]
    private string _guestLanguage = "";

    [ObservableProperty]
    private DateTime _arrival;

    [ObservableProperty]
    private DateTime _departure;

    [ObservableProperty]
    private bool _cancelled;
}

/// <summary>Backs the Manual Bookings screen: guests entered by hand (no Smoobu listing) flow
/// through the same access-provisioning pipeline as Smoobu reservations, with the guest-info
/// request emailed instead of sent via Smoobu, and replies always routed to the Dashboard's
/// manual-review queue since there's no way to detect an email reply automatically.</summary>
public sealed partial class ManualBookingsViewModel : ObservableObject
{
    private readonly IManualBookingStore _bookingStore;
    private readonly IApartmentMappingStore _mappingStore;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _newGuestFirstName = "";

    [ObservableProperty]
    private string _newGuestLastName = "";

    [ObservableProperty]
    private string _newGuestEmail = "";

    [ObservableProperty]
    private string _newGuestLanguage = "";

    [ObservableProperty]
    private ApartmentOption? _newApartment;

    [ObservableProperty]
    private DateTime? _newArrival = DateTime.Today.AddDays(1);

    [ObservableProperty]
    private DateTime? _newDeparture = DateTime.Today.AddDays(4);

    public ObservableCollection<ManualBookingRowViewModel> Bookings { get; } = new();
    public ObservableCollection<ApartmentOption> Apartments { get; } = new();

    public ManualBookingsViewModel(IManualBookingStore bookingStore, IApartmentMappingStore mappingStore)
    {
        _bookingStore = bookingStore;
        _mappingStore = mappingStore;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var mappings = await _mappingStore.GetAllAsync();
            Apartments.Clear();
            foreach (var mapping in mappings.OrderBy(m => m.ApartmentName))
            {
                Apartments.Add(new ApartmentOption { Id = mapping.SmoobuApartmentId, Name = mapping.ApartmentName });
            }
            NewApartment ??= Apartments.FirstOrDefault();

            var bookings = await _bookingStore.GetAllAsync();
            Bookings.Clear();
            foreach (var booking in bookings.OrderBy(b => b.Arrival))
            {
                Bookings.Add(ToRow(booking));
            }

            StatusMessage = $"Loaded {bookings.Count} manual booking(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load manual bookings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (NewApartment is null)
        {
            StatusMessage = "Choose an apartment first (refresh the Apartments tab from Smoobu if the list is empty).";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewGuestFirstName) || string.IsNullOrWhiteSpace(NewGuestEmail))
        {
            StatusMessage = "A first name and an email address are required.";
            return;
        }

        if (NewArrival is null || NewDeparture is null)
        {
            StatusMessage = "Choose an arrival and departure date.";
            return;
        }

        var arrival = DateOnly.FromDateTime(NewArrival.Value);
        var departure = DateOnly.FromDateTime(NewDeparture.Value);
        if (departure <= arrival)
        {
            StatusMessage = "Departure must be after arrival.";
            return;
        }

        IsBusy = true;
        try
        {
            var booking = new ManualBooking
            {
                Id = 0,
                ApartmentId = NewApartment.Id,
                ApartmentName = NewApartment.Name,
                GuestFirstName = NewGuestFirstName.Trim(),
                GuestLastName = NewGuestLastName.Trim(),
                GuestEmail = NewGuestEmail.Trim(),
                GuestLanguage = string.IsNullOrWhiteSpace(NewGuestLanguage) ? null : NewGuestLanguage.Trim(),
                Arrival = arrival,
                Departure = departure,
            };

            var id = await _bookingStore.AddAsync(booking);
            Bookings.Add(ToRow(booking with { Id = id }));

            NewGuestFirstName = "";
            NewGuestLastName = "";
            NewGuestEmail = "";
            NewGuestLanguage = "";
            NewArrival = DateTime.Today.AddDays(1);
            NewDeparture = DateTime.Today.AddDays(4);

            StatusMessage = "Booking added. It'll be emailed automatically on the next sync (per the lead-days setting).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't add booking: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleCancelledAsync(ManualBookingRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _bookingStore.SetCancelledAsync(row.Id, row.Cancelled);
            StatusMessage = row.Cancelled
                ? $"Cancelled {row.GuestFirstName} {row.GuestLastName} - access will be revoked on the next sync if it was already granted."
                : $"Reinstated {row.GuestFirstName} {row.GuestLastName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't update booking: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ManualBookingRowViewModel ToRow(ManualBooking booking) => new()
    {
        Id = booking.Id,
        ApartmentId = booking.ApartmentId,
        ApartmentName = booking.ApartmentName,
        GuestFirstName = booking.GuestFirstName,
        GuestLastName = booking.GuestLastName,
        GuestEmail = booking.GuestEmail,
        GuestLanguage = booking.GuestLanguage ?? "",
        Arrival = booking.Arrival.ToDateTime(TimeOnly.MinValue),
        Departure = booking.Departure.ToDateTime(TimeOnly.MinValue),
        Cancelled = booking.Cancelled,
    };
}
