using System.IO.Compression;
using System.Text;
using System.Text.Json;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Backup;

/// <summary>
/// Exports/imports application configuration (settings, message templates, automation webhooks,
/// apartment↔UniFi mappings, test-mode rules) as a zip bundle. Credentials (Smoobu API key, UniFi
/// Access token, SMTP password) are excluded by default; opting in AES-encrypts them into a
/// separate zip entry with a passphrase that must be supplied again on import.
/// </summary>
public sealed class BackupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly IAppSettingsStore _settingsStore;
    private readonly IMessageTemplateStore _templateStore;
    private readonly IWebhookConfigStore _webhookStore;
    private readonly IApartmentMappingStore _mappingStore;
    private readonly ITestModeRuleStore _testModeStore;
    private readonly IChannelMessagingSettingsStore _channelSettingsStore;

    public BackupService(
        IAppSettingsStore settingsStore,
        IMessageTemplateStore templateStore,
        IWebhookConfigStore webhookStore,
        IApartmentMappingStore mappingStore,
        ITestModeRuleStore testModeStore,
        IChannelMessagingSettingsStore channelSettingsStore)
    {
        _settingsStore = settingsStore ?? throw new ArgumentNullException(nameof(settingsStore));
        _templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
        _webhookStore = webhookStore ?? throw new ArgumentNullException(nameof(webhookStore));
        _mappingStore = mappingStore ?? throw new ArgumentNullException(nameof(mappingStore));
        _testModeStore = testModeStore ?? throw new ArgumentNullException(nameof(testModeStore));
        _channelSettingsStore = channelSettingsStore ?? throw new ArgumentNullException(nameof(channelSettingsStore));
    }

    public async Task ExportAsync(string filePath, bool includeCredentials, string? passphrase, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (includeCredentials && string.IsNullOrWhiteSpace(passphrase))
        {
            throw new ArgumentException("A passphrase is required to include credentials in a backup.", nameof(passphrase));
        }

        var settings = await _settingsStore.GetAsync(ct).ConfigureAwait(false);
        var templates = await _templateStore.GetAllAsync(ct).ConfigureAwait(false);
        var webhooks = await _webhookStore.GetAllAsync(ct).ConfigureAwait(false);
        var mappings = await _mappingStore.GetAllAsync(ct).ConfigureAwait(false);
        var rules = await _testModeStore.GetAllAsync(ct).ConfigureAwait(false);
        var channelSettings = await _channelSettingsStore.GetAllAsync(ct).ConfigureAwait(false);

        // Scrub credentials from the plain settings export; they only ever appear (encrypted) in
        // secrets.enc, and only when the caller opted in.
        var scrubbedSettings = new AppSettings
        {
            SmoobuApiKey = null,
            UnifiAccessHost = settings.UnifiAccessHost,
            UnifiAccessApiToken = null,
            UnifiAccessTrustAnySslCert = settings.UnifiAccessTrustAnySslCert,
            PollingIntervalMinutes = settings.PollingIntervalMinutes,
            MessageLeadDays = settings.MessageLeadDays,
            DefaultTemplateLanguage = settings.DefaultTemplateLanguage,
            TestModeEnabled = settings.TestModeEnabled,
            AutoApproveParsedReplies = settings.AutoApproveParsedReplies,
            GuestMessagingEnabled = settings.GuestMessagingEnabled,
            LicensePlateCountryPrefixes = new List<string>(settings.LicensePlateCountryPrefixes),
            Smtp = settings.Smtp is null ? null : new SmtpSettings
            {
                Host = settings.Smtp.Host,
                Port = settings.Smtp.Port,
                UseSsl = settings.Smtp.UseSsl,
                Username = settings.Smtp.Username,
                Password = null,
                FromAddress = settings.Smtp.FromAddress,
                ToAddress = settings.Smtp.ToAddress,
            },
        };

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Create);

        await WriteJsonEntryAsync(zip, "settings.json", scrubbedSettings, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "templates.json", templates, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "webhooks.json", webhooks, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "apartment-mappings.json", mappings, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "test-mode-rules.json", rules, ct).ConfigureAwait(false);
        await WriteJsonEntryAsync(zip, "channel-messaging-settings.json", channelSettings, ct).ConfigureAwait(false);

        if (includeCredentials)
        {
            var secrets = new ProtectedSecrets
            {
                SmoobuApiKey = settings.SmoobuApiKey,
                UnifiAccessApiToken = settings.UnifiAccessApiToken,
                SmtpPassword = settings.Smtp?.Password,
            };
            var secretsJson = JsonSerializer.Serialize(secrets, JsonOptions);
            var encrypted = BackupEncryption.Encrypt(secretsJson, passphrase!);

            var entry = zip.CreateEntry("secrets.enc", CompressionLevel.NoCompression);
            await using var entryStream = entry.Open();
            await entryStream.WriteAsync(encrypted, ct).ConfigureAwait(false);
        }

        var manifest = new BackupManifest
        {
            SchemaVersion = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            HasEncryptedSecrets = includeCredentials,
        };
        await WriteJsonEntryAsync(zip, "manifest.json", manifest, ct).ConfigureAwait(false);
    }

    public async Task<BackupPreview> PreviewImportAsync(string filePath, CancellationToken ct = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var manifest = await ReadJsonEntryAsync<BackupManifest>(zip, "manifest.json", ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("This file is not a valid UniFi Smoobu Tool backup (missing manifest.json).");

        var templates = await ReadJsonEntryAsync<List<MessageTemplate>>(zip, "templates.json", ct).ConfigureAwait(false) ?? new();
        var webhooks = await ReadJsonEntryAsync<List<WebhookConfig>>(zip, "webhooks.json", ct).ConfigureAwait(false) ?? new();
        var mappings = await ReadJsonEntryAsync<List<ApartmentAccessMapping>>(zip, "apartment-mappings.json", ct).ConfigureAwait(false) ?? new();
        var rules = await ReadJsonEntryAsync<List<TestModeRule>>(zip, "test-mode-rules.json", ct).ConfigureAwait(false) ?? new();
        var channelSettings = await ReadJsonEntryAsync<List<ChannelMessagingSetting>>(zip, "channel-messaging-settings.json", ct).ConfigureAwait(false) ?? new();

        return new BackupPreview
        {
            SchemaVersion = manifest.SchemaVersion,
            CreatedAtUtc = manifest.CreatedAtUtc,
            HasEncryptedSecrets = manifest.HasEncryptedSecrets,
            TemplateCount = templates.Count,
            WebhookCount = webhooks.Count,
            ApartmentMappingCount = mappings.Count,
            TestModeRuleCount = rules.Count,
            ChannelSettingCount = channelSettings.Count,
        };
    }

    public async Task ImportAsync(string filePath, string? passphrase, CancellationToken ct = default)
    {
        using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var zip = new ZipArchive(fileStream, ZipArchiveMode.Read);

        var manifest = await ReadJsonEntryAsync<BackupManifest>(zip, "manifest.json", ct).ConfigureAwait(false)
            ?? throw new InvalidDataException("This file is not a valid UniFi Smoobu Tool backup (missing manifest.json).");

        if (manifest.SchemaVersion > 1)
        {
            throw new InvalidDataException(
                $"This backup was created by a newer version of the app (schema {manifest.SchemaVersion}) and can't be imported here.");
        }

        var settings = await ReadJsonEntryAsync<AppSettings>(zip, "settings.json", ct).ConfigureAwait(false) ?? new AppSettings();

        if (manifest.HasEncryptedSecrets)
        {
            if (string.IsNullOrWhiteSpace(passphrase))
            {
                throw new ArgumentException("This backup includes encrypted credentials; a passphrase is required to import it.", nameof(passphrase));
            }

            var entry = zip.GetEntry("secrets.enc")
                ?? throw new InvalidDataException("Backup manifest says credentials were included, but secrets.enc is missing.");

            await using var entryStream = entry.Open();
            using var buffer = new MemoryStream();
            await entryStream.CopyToAsync(buffer, ct).ConfigureAwait(false);

            var json = BackupEncryption.Decrypt(buffer.ToArray(), passphrase);
            var secrets = JsonSerializer.Deserialize<ProtectedSecrets>(json, JsonOptions);

            settings.SmoobuApiKey = secrets?.SmoobuApiKey;
            settings.UnifiAccessApiToken = secrets?.UnifiAccessApiToken;
            if (settings.Smtp is not null && secrets?.SmtpPassword is not null)
            {
                settings.Smtp = new SmtpSettings
                {
                    Host = settings.Smtp.Host,
                    Port = settings.Smtp.Port,
                    UseSsl = settings.Smtp.UseSsl,
                    Username = settings.Smtp.Username,
                    Password = secrets.SmtpPassword,
                    FromAddress = settings.Smtp.FromAddress,
                    ToAddress = settings.Smtp.ToAddress,
                };
            }
        }

        await _settingsStore.SaveAsync(settings, ct).ConfigureAwait(false);

        var templates = await ReadJsonEntryAsync<List<MessageTemplate>>(zip, "templates.json", ct).ConfigureAwait(false) ?? new();
        foreach (var template in templates)
        {
            await _templateStore.SaveAsync(template, ct).ConfigureAwait(false);
        }

        var webhooks = await ReadJsonEntryAsync<List<WebhookConfig>>(zip, "webhooks.json", ct).ConfigureAwait(false) ?? new();
        foreach (var webhook in webhooks)
        {
            await _webhookStore.SaveAsync(webhook, ct).ConfigureAwait(false);
        }

        var mappings = await ReadJsonEntryAsync<List<ApartmentAccessMapping>>(zip, "apartment-mappings.json", ct).ConfigureAwait(false) ?? new();
        foreach (var mapping in mappings)
        {
            await _mappingStore.SaveAsync(mapping, ct).ConfigureAwait(false);
        }

        var rules = await ReadJsonEntryAsync<List<TestModeRule>>(zip, "test-mode-rules.json", ct).ConfigureAwait(false) ?? new();
        foreach (var rule in rules)
        {
            await _testModeStore.SaveAsync(rule, ct).ConfigureAwait(false);
        }

        var channelSettings = await ReadJsonEntryAsync<List<ChannelMessagingSetting>>(zip, "channel-messaging-settings.json", ct).ConfigureAwait(false) ?? new();
        foreach (var channelSetting in channelSettings)
        {
            await _channelSettingsStore.SaveAsync(channelSetting, ct).ConfigureAwait(false);
        }
    }

    private static async Task WriteJsonEntryAsync<T>(ZipArchive zip, string entryName, T value, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct).ConfigureAwait(false);
    }

    private static async Task<T?> ReadJsonEntryAsync<T>(ZipArchive zip, string entryName, CancellationToken ct)
    {
        var entry = zip.GetEntry(entryName);
        if (entry is null)
        {
            return default;
        }

        await using var stream = entry.Open();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false);
    }
}
