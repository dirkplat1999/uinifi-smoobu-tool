using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class ApartmentOption : ObservableObject
{
    public required int Id { get; init; }
    public required string Name { get; init; }
}

public sealed partial class WebhookRowViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private int _apartmentId;

    [ObservableProperty]
    private string _name = "New automation";

    [ObservableProperty]
    private AutomationTrigger _trigger = AutomationTrigger.AccessGranted;

    [ObservableProperty]
    private WebhookMethod _method = WebhookMethod.Get;

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _payloadTemplate = "";

    [ObservableProperty]
    private bool _enabled = true;
}

/// <summary>
/// Backs the Automation Webhooks screen (Feature 7): per-apartment universal webhooks with a
/// selectable trigger event, HTTP method, and a placeholder-driven payload/URL template - e.g. to
/// greet a guest by name on a Chromecast or smart display once their access is granted.
/// </summary>
public sealed partial class WebhooksViewModel : ObservableObject
{
    private readonly IWebhookConfigStore _webhookStore;
    private readonly IApartmentMappingStore _mappingStore;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private WebhookRowViewModel? _selectedWebhook;

    public ObservableCollection<WebhookRowViewModel> Webhooks { get; } = new();
    public ObservableCollection<ApartmentOption> Apartments { get; } = new();

    public AutomationTrigger[] TriggerOptions { get; } =
        Enum.GetValues<AutomationTrigger>().Where(t => t != AutomationTrigger.ErrorOccurred).ToArray();

    public WebhookMethod[] MethodOptions { get; } = Enum.GetValues<WebhookMethod>();

    public string PlaceholderHelp =>
        "URL and payload can both use {{guest_first_name}}, {{guest_last_name}}, {{guest_full_name}}, " +
        "{{apartment_name}}, {{arrival_date}}, {{departure_date}}, {{reservation_id}}. " +
        "GET requests substitute placeholders (URL-encoded) into the URL; POST requests also send the payload as the request body.";

    public WebhooksViewModel(IWebhookConfigStore webhookStore, IApartmentMappingStore mappingStore)
    {
        _webhookStore = webhookStore;
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

            var all = await _webhookStore.GetAllAsync();
            Webhooks.Clear();
            foreach (var config in all.Where(c => c.ApartmentId is not null))
            {
                Webhooks.Add(new WebhookRowViewModel
                {
                    Id = config.Id,
                    ApartmentId = config.ApartmentId!.Value,
                    Name = config.Name,
                    Trigger = config.Trigger,
                    Method = config.Method,
                    Url = config.Url,
                    PayloadTemplate = config.PayloadTemplate ?? "",
                    Enabled = config.Enabled,
                });
            }

            StatusMessage = $"Loaded {Webhooks.Count} automation webhook(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load webhooks: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Add()
    {
        var row = new WebhookRowViewModel { ApartmentId = Apartments.FirstOrDefault()?.Id ?? 0 };
        Webhooks.Add(row);
        SelectedWebhook = row;
    }

    [RelayCommand]
    private async Task SaveAsync(WebhookRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _webhookStore.SaveAsync(new WebhookConfig
            {
                Id = row.Id,
                ApartmentId = row.ApartmentId,
                Name = row.Name,
                Trigger = row.Trigger,
                Method = row.Method,
                Url = row.Url,
                PayloadTemplate = string.IsNullOrWhiteSpace(row.PayloadTemplate) ? null : row.PayloadTemplate,
                Enabled = row.Enabled,
            });
            StatusMessage = $"Saved \"{row.Name}\".";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save webhook: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(WebhookRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _webhookStore.DeleteAsync(row.Id);
            Webhooks.Remove(row);
            StatusMessage = $"Deleted \"{row.Name}\".";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't delete webhook: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
