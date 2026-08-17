using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.App.ViewModels;

public sealed partial class ErrorWebhookRowViewModel : ObservableObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    [ObservableProperty]
    private string _name = "New alert";

    [ObservableProperty]
    private WebhookMethod _method = WebhookMethod.Post;

    [ObservableProperty]
    private string _url = "";

    [ObservableProperty]
    private string _payloadTemplate = "{\"component\":\"{{component}}\",\"message\":\"{{message}}\"}";

    [ObservableProperty]
    private bool _enabled = true;
}

/// <summary>
/// Backs the Settings screen: Smoobu/UniFi Access connection details (Features 1-6), sync
/// behavior, license plate country-prefix stripping (Feature 3), SMTP alerting (Feature 14), and
/// error webhooks for external automation systems like Homey Pro (Feature 15).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IWebhookConfigStore _webhookStore;

    [ObservableProperty]
    private string _smoobuApiKey = "";

    [ObservableProperty]
    private string _unifiAccessHost = "";

    [ObservableProperty]
    private string _unifiAccessApiToken = "";

    [ObservableProperty]
    private bool _unifiAccessTrustAnySslCert = true;

    [ObservableProperty]
    private int _pollingIntervalMinutes = 10;

    [ObservableProperty]
    private int _messageLeadDays = 3;

    [ObservableProperty]
    private string _defaultTemplateLanguage = "en";

    [ObservableProperty]
    private bool _autoApproveParsedReplies = true;

    [ObservableProperty]
    private string _licensePlateCountryPrefixesText = "";

    [ObservableProperty]
    private bool _smtpEnabled;

    [ObservableProperty]
    private string _smtpHost = "";

    [ObservableProperty]
    private int _smtpPort = 587;

    [ObservableProperty]
    private bool _smtpUseSsl = true;

    [ObservableProperty]
    private string _smtpUsername = "";

    [ObservableProperty]
    private string _smtpPassword = "";

    [ObservableProperty]
    private string _smtpFromAddress = "";

    [ObservableProperty]
    private string _smtpToAddress = "";

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private bool _isBusy;

    public ObservableCollection<ErrorWebhookRowViewModel> ErrorWebhooks { get; } = new();
    public WebhookMethod[] MethodOptions { get; } = Enum.GetValues<WebhookMethod>();

    public SettingsViewModel(IAppSettingsStore settingsStore, IWebhookConfigStore webhookStore)
    {
        _settingsStore = settingsStore;
        _webhookStore = webhookStore;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsStore.GetAsync();
            SmoobuApiKey = settings.SmoobuApiKey ?? "";
            UnifiAccessHost = settings.UnifiAccessHost ?? "";
            UnifiAccessApiToken = settings.UnifiAccessApiToken ?? "";
            UnifiAccessTrustAnySslCert = settings.UnifiAccessTrustAnySslCert;
            PollingIntervalMinutes = settings.PollingIntervalMinutes;
            MessageLeadDays = settings.MessageLeadDays;
            DefaultTemplateLanguage = settings.DefaultTemplateLanguage;
            AutoApproveParsedReplies = settings.AutoApproveParsedReplies;
            LicensePlateCountryPrefixesText = string.Join(", ", settings.LicensePlateCountryPrefixes);

            SmtpEnabled = settings.Smtp is not null;
            SmtpHost = settings.Smtp?.Host ?? "";
            SmtpPort = settings.Smtp?.Port ?? 587;
            SmtpUseSsl = settings.Smtp?.UseSsl ?? true;
            SmtpUsername = settings.Smtp?.Username ?? "";
            SmtpPassword = settings.Smtp?.Password ?? "";
            SmtpFromAddress = settings.Smtp?.FromAddress ?? "";
            SmtpToAddress = settings.Smtp?.ToAddress ?? "";

            var errorWebhooks = await _webhookStore.GetErrorWebhooksAsync();
            ErrorWebhooks.Clear();
            foreach (var config in errorWebhooks)
            {
                ErrorWebhooks.Add(new ErrorWebhookRowViewModel
                {
                    Id = config.Id,
                    Name = config.Name,
                    Method = config.Method,
                    Url = config.Url,
                    PayloadTemplate = config.PayloadTemplate ?? "",
                    Enabled = config.Enabled,
                });
            }

            StatusMessage = "Settings loaded.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't load settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsStore.GetAsync();
            settings.SmoobuApiKey = string.IsNullOrWhiteSpace(SmoobuApiKey) ? null : SmoobuApiKey.Trim();
            settings.UnifiAccessHost = string.IsNullOrWhiteSpace(UnifiAccessHost) ? null : UnifiAccessHost.Trim();
            settings.UnifiAccessApiToken = string.IsNullOrWhiteSpace(UnifiAccessApiToken) ? null : UnifiAccessApiToken.Trim();
            settings.UnifiAccessTrustAnySslCert = UnifiAccessTrustAnySslCert;
            settings.PollingIntervalMinutes = Math.Max(1, PollingIntervalMinutes);
            settings.MessageLeadDays = Math.Max(0, MessageLeadDays);
            settings.DefaultTemplateLanguage = string.IsNullOrWhiteSpace(DefaultTemplateLanguage) ? "en" : DefaultTemplateLanguage.Trim();
            settings.AutoApproveParsedReplies = AutoApproveParsedReplies;
            settings.LicensePlateCountryPrefixes = LicensePlateCountryPrefixesText
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            settings.Smtp = SmtpEnabled
                ? new SmtpSettings
                {
                    Host = SmtpHost.Trim(),
                    Port = SmtpPort,
                    UseSsl = SmtpUseSsl,
                    Username = string.IsNullOrWhiteSpace(SmtpUsername) ? null : SmtpUsername.Trim(),
                    Password = string.IsNullOrWhiteSpace(SmtpPassword) ? null : SmtpPassword,
                    FromAddress = SmtpFromAddress.Trim(),
                    ToAddress = SmtpToAddress.Trim(),
                }
                : null;

            await _settingsStore.SaveAsync(settings);

            foreach (var row in ErrorWebhooks)
            {
                await _webhookStore.SaveAsync(new WebhookConfig
                {
                    Id = row.Id,
                    ApartmentId = null,
                    Name = row.Name,
                    Trigger = AutomationTrigger.ErrorOccurred,
                    Method = row.Method,
                    Url = row.Url,
                    PayloadTemplate = string.IsNullOrWhiteSpace(row.PayloadTemplate) ? null : row.PayloadTemplate,
                    Enabled = row.Enabled,
                });
            }

            StatusMessage = "Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't save settings: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void AddErrorWebhook()
    {
        ErrorWebhooks.Add(new ErrorWebhookRowViewModel());
    }

    [RelayCommand]
    private async Task DeleteErrorWebhookAsync(ErrorWebhookRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _webhookStore.DeleteAsync(row.Id);
            ErrorWebhooks.Remove(row);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't remove alert: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
