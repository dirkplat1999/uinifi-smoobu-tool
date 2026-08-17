using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class TemplateRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _languageCode;

    [ObservableProperty]
    private string _body;

    public TemplateRowViewModel(string languageCode, string body)
    {
        _languageCode = languageCode;
        _body = body;
    }
}

/// <summary>Backs the Message Templates screen (Feature 13): one guest-info-request template per
/// language code, selected automatically at send time based on the guest's Smoobu language.</summary>
public sealed partial class TemplatesViewModel : ObservableObject
{
    private readonly IMessageTemplateStore _templateStore;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private TemplateRowViewModel? _selectedTemplate;

    [ObservableProperty]
    private string _newLanguageCode = "";

    public ObservableCollection<TemplateRowViewModel> Templates { get; } = new();

    public string PlaceholderHelp =>
        "Available placeholders: {{guest_first_name}}, {{guest_last_name}}, {{guest_full_name}}, " +
        "{{apartment_name}}, {{arrival_date}}, {{departure_date}}, {{reservation_id}}";

    public TemplatesViewModel(IMessageTemplateStore templateStore)
    {
        _templateStore = templateStore;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var templates = await _templateStore.GetAllAsync();
            Templates.Clear();
            foreach (var template in templates.OrderBy(t => t.LanguageCode))
            {
                Templates.Add(new TemplateRowViewModel(template.LanguageCode, template.Body));
            }
            StatusMessage = $"Loaded {templates.Count} template(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load templates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var code = NewLanguageCode.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusMessage = "Enter a language code first (e.g. \"en\", \"nl\", \"de\").";
            return;
        }

        if (Templates.Any(t => t.LanguageCode == code))
        {
            StatusMessage = $"A template for \"{code}\" already exists.";
            return;
        }

        var row = new TemplateRowViewModel(code, "Hi {{guest_first_name}}, could you send us your license plate and a 4-digit PIN before arrival?");
        Templates.Add(row);
        SelectedTemplate = row;
        NewLanguageCode = "";
        await SaveAsync();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var row in Templates)
            {
                await _templateStore.SaveAsync(new MessageTemplate { LanguageCode = row.LanguageCode, Body = row.Body });
            }
            StatusMessage = "Templates saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save templates: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(TemplateRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _templateStore.DeleteAsync(row.LanguageCode);
            Templates.Remove(row);
            StatusMessage = $"Deleted the \"{row.LanguageCode}\" template.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't delete template: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
