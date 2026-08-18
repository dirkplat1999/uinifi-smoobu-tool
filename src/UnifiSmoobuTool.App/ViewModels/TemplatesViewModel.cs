using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

/// <summary>A selectable language option in the "Add template" pickers - either one of the common
/// languages the app ships default templates for, or a free-text custom code.</summary>
public sealed record LanguageOption(string Code, string DisplayName)
{
    public const string CustomCode = "";

    public override string ToString() => Code == CustomCode ? DisplayName : $"{DisplayName} ({Code})";
}

public sealed partial class TemplateRowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _languageCode;

    [ObservableProperty]
    private MessageTemplateKind _kind;

    [ObservableProperty]
    private string _body;

    public TemplateRowViewModel(string languageCode, MessageTemplateKind kind, string body)
    {
        _languageCode = languageCode;
        _kind = kind;
        _body = body;
    }

    public string DisplayName => $"{LanguageCode} - {Kind}";
}

/// <summary>Backs the Message Templates screen (Feature 13): a Request/Clarification/Confirmation
/// template per language code, selected automatically at send time based on the guest's Smoobu
/// language and what point in the guest-messaging flow the app is at.</summary>
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
    private LanguageOption _newLanguage;

    [ObservableProperty]
    private string _newCustomLanguageCode = "";

    [ObservableProperty]
    private MessageTemplateKind _newKind = MessageTemplateKind.Request;

    public ObservableCollection<TemplateRowViewModel> Templates { get; } = new();

    public LanguageOption[] LanguageOptions { get; } =
    {
        new("nl", "Nederlands"),
        new("en", "English"),
        new("de", "Deutsch"),
        new("fr", "Français"),
        new(LanguageOption.CustomCode, "Custom..."),
    };

    public MessageTemplateKind[] KindOptions { get; } = Enum.GetValues<MessageTemplateKind>();

    public string PlaceholderHelp =>
        "Available placeholders: {{guest_first_name}}, {{guest_last_name}}, {{guest_full_name}}, " +
        "{{apartment_name}}, {{arrival_date}}, {{departure_date}}, {{reservation_id}}. " +
        "Request is the initial arrival ask, Clarification is auto-sent when a reply can't be read " +
        "clearly, Confirmation is auto-sent when it can.";

    public TemplatesViewModel(IMessageTemplateStore templateStore)
    {
        _templateStore = templateStore;
        _newLanguage = LanguageOptions[0];
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var templates = await _templateStore.GetAllAsync();
            Templates.Clear();
            foreach (var template in templates.OrderBy(t => t.LanguageCode).ThenBy(t => t.Kind))
            {
                Templates.Add(new TemplateRowViewModel(template.LanguageCode, template.Kind, template.Body));
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
        var code = (NewLanguage.Code == LanguageOption.CustomCode ? NewCustomLanguageCode : NewLanguage.Code)
            .Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(code))
        {
            StatusMessage = "Enter a language code first (e.g. \"en\", \"nl\", \"de\").";
            return;
        }

        if (Templates.Any(t => t.LanguageCode == code && t.Kind == NewKind))
        {
            StatusMessage = $"A \"{NewKind}\" template for \"{code}\" already exists.";
            return;
        }

        var row = new TemplateRowViewModel(code, NewKind, "Hi {{guest_first_name}}, could you send us your license plate and a 4-digit PIN before arrival?");
        Templates.Add(row);
        SelectedTemplate = row;
        NewCustomLanguageCode = "";
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
                await _templateStore.SaveAsync(new MessageTemplate { LanguageCode = row.LanguageCode, Kind = row.Kind, Body = row.Body });
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
            await _templateStore.DeleteAsync(row.LanguageCode, row.Kind);
            Templates.Remove(row);
            StatusMessage = $"Deleted the \"{row.LanguageCode}\" {row.Kind} template.";
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
