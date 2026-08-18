using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;
using UnifiSmoobuTool.Infrastructure.Notifications;
using UnifiSmoobuTool.Infrastructure.Startup;

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

public sealed partial class ChannelSettingRowViewModel : ObservableObject
{
    public required string ChannelName { get; init; }

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
    private readonly IChannelMessagingSettingsStore _channelSettingsStore;
    private readonly ISmoobuClient _smoobuClient;
    private readonly IUnifiAccessClient _unifiAccessClient;
    private readonly SmtpAlerter _smtpAlerter;
    private readonly WebhookDispatcher _webhookDispatcher;

    [ObservableProperty]
    private bool _runInBackgroundWhenClosed = true;

    [ObservableProperty]
    private bool _startWithWindows;

    public bool StartWithWindowsSupported => WindowsStartupManager.IsSupported;

    [ObservableProperty]
    private string _smoobuApiKey = "";

    [ObservableProperty]
    private string _smoobuApiSecret = "";

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
    private bool _guestMessagingEnabled = true;

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

    [ObservableProperty]
    private bool _hasChannelSettings;

    public ObservableCollection<ErrorWebhookRowViewModel> ErrorWebhooks { get; } = new();
    public ObservableCollection<ChannelSettingRowViewModel> ChannelSettings { get; } = new();
    public WebhookMethod[] MethodOptions { get; } = Enum.GetValues<WebhookMethod>();

    public SettingsViewModel(
        IAppSettingsStore settingsStore,
        IWebhookConfigStore webhookStore,
        IChannelMessagingSettingsStore channelSettingsStore,
        ISmoobuClient smoobuClient,
        IUnifiAccessClient unifiAccessClient,
        SmtpAlerter smtpAlerter,
        WebhookDispatcher webhookDispatcher)
    {
        _settingsStore = settingsStore;
        _webhookStore = webhookStore;
        _channelSettingsStore = channelSettingsStore;
        _smoobuClient = smoobuClient;
        _unifiAccessClient = unifiAccessClient;
        _smtpAlerter = smtpAlerter;
        _webhookDispatcher = webhookDispatcher;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await _settingsStore.GetAsync();
            RunInBackgroundWhenClosed = settings.RunInBackgroundWhenClosed;
            StartWithWindows = WindowsStartupManager.IsSupported && WindowsStartupManager.IsEnabled();
            SmoobuApiKey = settings.SmoobuApiKey ?? "";
            SmoobuApiSecret = settings.SmoobuApiSecret ?? "";
            UnifiAccessHost = settings.UnifiAccessHost ?? "";
            UnifiAccessApiToken = settings.UnifiAccessApiToken ?? "";
            UnifiAccessTrustAnySslCert = settings.UnifiAccessTrustAnySslCert;
            PollingIntervalMinutes = settings.PollingIntervalMinutes;
            MessageLeadDays = settings.MessageLeadDays;
            DefaultTemplateLanguage = settings.DefaultTemplateLanguage;
            AutoApproveParsedReplies = settings.AutoApproveParsedReplies;
            GuestMessagingEnabled = settings.GuestMessagingEnabled;
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

            var channelSettings = await _channelSettingsStore.GetAllAsync();
            ChannelSettings.Clear();
            foreach (var setting in channelSettings)
            {
                ChannelSettings.Add(new ChannelSettingRowViewModel { ChannelName = setting.ChannelName, Enabled = setting.Enabled });
            }
            HasChannelSettings = ChannelSettings.Count > 0;

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
            settings.RunInBackgroundWhenClosed = RunInBackgroundWhenClosed;
            settings.SmoobuApiKey = string.IsNullOrWhiteSpace(SmoobuApiKey) ? null : SmoobuApiKey.Trim();
            settings.SmoobuApiSecret = string.IsNullOrWhiteSpace(SmoobuApiSecret) ? null : SmoobuApiSecret.Trim();
            settings.UnifiAccessHost = string.IsNullOrWhiteSpace(UnifiAccessHost) ? null : UnifiAccessHost.Trim();
            settings.UnifiAccessApiToken = string.IsNullOrWhiteSpace(UnifiAccessApiToken) ? null : UnifiAccessApiToken.Trim();
            settings.UnifiAccessTrustAnySslCert = UnifiAccessTrustAnySslCert;
            settings.PollingIntervalMinutes = Math.Max(1, PollingIntervalMinutes);
            settings.MessageLeadDays = Math.Max(0, MessageLeadDays);
            settings.DefaultTemplateLanguage = string.IsNullOrWhiteSpace(DefaultTemplateLanguage) ? "en" : DefaultTemplateLanguage.Trim();
            settings.AutoApproveParsedReplies = AutoApproveParsedReplies;
            settings.GuestMessagingEnabled = GuestMessagingEnabled;
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

            if (WindowsStartupManager.IsSupported)
            {
                try
                {
                    WindowsStartupManager.SetEnabled(StartWithWindows);
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Settings saved, but couldn't update the Windows startup entry: {ex.Message}";
                }
            }

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

            foreach (var row in ChannelSettings)
            {
                await _channelSettingsStore.SaveAsync(new ChannelMessagingSetting { ChannelName = row.ChannelName, Enabled = row.Enabled });
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

    /// <summary>Saves first so the test always exercises what's currently on screen, not
    /// whatever was last persisted.</summary>
    [RelayCommand]
    private async Task TestSmoobuConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving settings and testing the Smoobu connection...";
        try
        {
            await SaveAsync();
            var apartments = await _smoobuClient.GetApartmentsAsync();
            StatusMessage = $"Smoobu connection OK - found {apartments.Count} apartment(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Smoobu connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestUnifiAccessConnectionAsync()
    {
        IsBusy = true;
        StatusMessage = "Saving settings and testing the UniFi Access connection...";
        try
        {
            await SaveAsync();
            var resources = await _unifiAccessClient.GetDoorGroupTopologyAsync();
            StatusMessage = $"UniFi Access connection OK - found {resources.Count} door/door-group resource(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"UniFi Access connection test failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task TestSmtpAsync()
    {
        if (!SmtpEnabled)
        {
            StatusMessage = "Enable and fill in SMTP settings first.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Saving settings and sending a test email...";
        try
        {
            await SaveAsync();
            var settings = await _settingsStore.GetAsync();
            if (settings.Smtp is null)
            {
                StatusMessage = "SMTP is not configured.";
                return;
            }

            await _smtpAlerter.SendAsync(
                settings.Smtp, "UniFi Smoobu Tool - test email",
                "This is a test email to confirm your SMTP settings work.");
            StatusMessage = $"Test email sent to {settings.Smtp.ToAddress}. Check the Logs tab if it doesn't arrive.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't send test email: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SendTestErrorWebhookAsync(ErrorWebhookRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(row.Url))
        {
            StatusMessage = "Enter a URL for this alert first.";
            return;
        }

        IsBusy = true;
        try
        {
            var config = new WebhookConfig
            {
                Id = row.Id,
                ApartmentId = null,
                Name = row.Name,
                Trigger = AutomationTrigger.ErrorOccurred,
                Method = row.Method,
                Url = row.Url,
                PayloadTemplate = string.IsNullOrWhiteSpace(row.PayloadTemplate) ? null : row.PayloadTemplate,
                Enabled = true,
            };

            var placeholders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["component"] = "Test",
                ["message"] = "This is a test alert from UniFi Smoobu Tool.",
                ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            };

            await _webhookDispatcher.DispatchAsync(config, placeholders);
            StatusMessage = $"Sent a test alert to \"{row.Name}\". Check the target system (and the Logs tab if nothing arrived).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Couldn't send test alert: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
