using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class ApartmentMappingRowViewModel : ObservableObject
{
    public required int ApartmentId { get; init; }
    public required string ApartmentName { get; init; }

    public List<UnifiResourceRef> AssignedResources { get; set; } = new();

    [ObservableProperty]
    private string _assignedResourcesSummary = "(none)";
}

public sealed partial class SelectableResourceViewModel : ObservableObject
{
    public required UnifiResourceRef Resource { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>Backs the Apartments screen: refreshing the apartment list from Smoobu (Feature 6) and
/// mapping each apartment to the UniFi Access doors/door groups its guests should be able to reach.</summary>
public sealed partial class ApartmentsViewModel : ObservableObject
{
    private readonly ISmoobuClient _smoobu;
    private readonly IUnifiAccessClient _unifi;
    private readonly IApartmentMappingStore _mappingStore;

    [ObservableProperty]
    private string _statusMessage = "Click \"Refresh from Smoobu\" to load your apartments.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private ApartmentMappingRowViewModel? _selectedApartment;

    public ObservableCollection<ApartmentMappingRowViewModel> Apartments { get; } = new();
    public ObservableCollection<SelectableResourceViewModel> AvailableResources { get; } = new();

    public ApartmentsViewModel(ISmoobuClient smoobu, IUnifiAccessClient unifi, IApartmentMappingStore mappingStore)
    {
        _smoobu = smoobu;
        _unifi = unifi;
        _mappingStore = mappingStore;
    }

    partial void OnSelectedApartmentChanged(ApartmentMappingRowViewModel? value)
    {
        foreach (var resource in AvailableResources)
        {
            resource.IsSelected = value?.AssignedResources.Any(a => a.Id == resource.Resource.Id) ?? false;
        }
    }

    [RelayCommand]
    private async Task RefreshFromSmoobuAsync()
    {
        IsBusy = true;
        StatusMessage = "Refreshing apartments from Smoobu...";
        try
        {
            var apartments = await _smoobu.GetApartmentsAsync();
            var mappings = await _mappingStore.GetAllAsync();

            Apartments.Clear();
            foreach (var apartment in apartments.OrderBy(a => a.Name))
            {
                var mapping = mappings.FirstOrDefault(m => m.SmoobuApartmentId == apartment.Id);
                Apartments.Add(new ApartmentMappingRowViewModel
                {
                    ApartmentId = apartment.Id,
                    ApartmentName = apartment.Name,
                    AssignedResources = mapping?.UnifiResources.ToList() ?? new List<UnifiResourceRef>(),
                    AssignedResourcesSummary = Summarize(mapping?.UnifiResources),
                });
            }

            StatusMessage = $"Loaded {apartments.Count} apartment(s) from Smoobu.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load apartments: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LoadUnifiResourcesAsync()
    {
        IsBusy = true;
        StatusMessage = "Loading UniFi Access doors...";
        try
        {
            var resources = await _unifi.GetDoorGroupTopologyAsync();
            AvailableResources.Clear();
            foreach (var resource in resources)
            {
                AvailableResources.Add(new SelectableResourceViewModel { Resource = resource });
            }
            OnSelectedApartmentChanged(SelectedApartment);

            StatusMessage = $"Loaded {resources.Count} UniFi Access resource(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load UniFi Access resources: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveMappingAsync()
    {
        if (SelectedApartment is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var selected = AvailableResources.Where(r => r.IsSelected).Select(r => r.Resource).ToList();
            await _mappingStore.SaveAsync(new ApartmentAccessMapping
            {
                SmoobuApartmentId = SelectedApartment.ApartmentId,
                ApartmentName = SelectedApartment.ApartmentName,
                UnifiResources = selected,
            });

            SelectedApartment.AssignedResources = selected;
            SelectedApartment.AssignedResourcesSummary = Summarize(selected);
            StatusMessage = $"Saved access mapping for {SelectedApartment.ApartmentName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save mapping: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static string Summarize(IReadOnlyCollection<UnifiResourceRef>? resources) =>
        resources is null || resources.Count == 0 ? "(none)" : string.Join(", ", resources.Select(r => r.Name));
}
