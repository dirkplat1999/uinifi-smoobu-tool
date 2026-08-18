using System.Text.Json;
using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Infrastructure.Security;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteAppSettingsStore : IAppSettingsStore
{
    private const string SelectColumns = """
        smoobu_api_key_protected AS SmoobuApiKeyProtected,
        smoobu_api_secret_protected AS SmoobuApiSecretProtected,
        unifi_access_host AS UnifiAccessHost,
        unifi_access_api_token_protected AS UnifiAccessApiTokenProtected,
        unifi_access_trust_any_ssl_cert AS UnifiAccessTrustAnySslCert,
        polling_interval_minutes AS PollingIntervalMinutes,
        message_lead_days AS MessageLeadDays,
        default_template_language AS DefaultTemplateLanguage,
        test_mode_enabled AS TestModeEnabled,
        auto_approve_parsed_replies AS AutoApproveParsedReplies,
        license_plate_country_prefixes_json AS LicensePlateCountryPrefixesJson,
        smtp_host AS SmtpHost,
        smtp_port AS SmtpPort,
        smtp_use_ssl AS SmtpUseSsl,
        smtp_username AS SmtpUsername,
        smtp_password_protected AS SmtpPasswordProtected,
        smtp_from_address AS SmtpFromAddress,
        smtp_to_address AS SmtpToAddress,
        run_in_background_when_closed AS RunInBackgroundWhenClosed
        """;

    private readonly SqliteConnectionFactory _factory;
    private readonly SecretProtector _protector;

    public SqliteAppSettingsStore(SqliteConnectionFactory factory, SecretProtector protector)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public async Task<AppSettings> GetAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SettingsRow>(
            $"SELECT {SelectColumns} FROM app_settings WHERE id = 1").ConfigureAwait(false);

        if (row is null)
        {
            return new AppSettings();
        }

        SmtpSettings? smtp = string.IsNullOrWhiteSpace(row.SmtpHost)
            ? null
            : new SmtpSettings
            {
                Host = row.SmtpHost,
                Port = row.SmtpPort ?? 587,
                UseSsl = row.SmtpUseSsl ?? true,
                Username = row.SmtpUsername,
                Password = _protector.Unprotect(row.SmtpPasswordProtected),
                FromAddress = row.SmtpFromAddress ?? string.Empty,
                ToAddress = row.SmtpToAddress ?? string.Empty,
            };

        return new AppSettings
        {
            SmoobuApiKey = _protector.Unprotect(row.SmoobuApiKeyProtected),
            SmoobuApiSecret = _protector.Unprotect(row.SmoobuApiSecretProtected),
            UnifiAccessHost = row.UnifiAccessHost,
            UnifiAccessApiToken = _protector.Unprotect(row.UnifiAccessApiTokenProtected),
            UnifiAccessTrustAnySslCert = row.UnifiAccessTrustAnySslCert,
            PollingIntervalMinutes = row.PollingIntervalMinutes,
            MessageLeadDays = row.MessageLeadDays,
            DefaultTemplateLanguage = row.DefaultTemplateLanguage,
            TestModeEnabled = row.TestModeEnabled,
            AutoApproveParsedReplies = row.AutoApproveParsedReplies,
            LicensePlateCountryPrefixes = string.IsNullOrWhiteSpace(row.LicensePlateCountryPrefixesJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(row.LicensePlateCountryPrefixesJson) ?? new List<string>(),
            Smtp = smtp,
            RunInBackgroundWhenClosed = row.RunInBackgroundWhenClosed,
        };
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO app_settings
                (id, smoobu_api_key_protected, smoobu_api_secret_protected, unifi_access_host, unifi_access_api_token_protected,
                 unifi_access_trust_any_ssl_cert, polling_interval_minutes, message_lead_days,
                 default_template_language, test_mode_enabled, auto_approve_parsed_replies,
                 license_plate_country_prefixes_json, smtp_host, smtp_port, smtp_use_ssl, smtp_username,
                 smtp_password_protected, smtp_from_address, smtp_to_address, run_in_background_when_closed)
            VALUES
                (1, @SmoobuApiKeyProtected, @SmoobuApiSecretProtected, @UnifiAccessHost, @UnifiAccessApiTokenProtected,
                 @UnifiAccessTrustAnySslCert, @PollingIntervalMinutes, @MessageLeadDays,
                 @DefaultTemplateLanguage, @TestModeEnabled, @AutoApproveParsedReplies,
                 @LicensePlateCountryPrefixesJson, @SmtpHost, @SmtpPort, @SmtpUseSsl, @SmtpUsername,
                 @SmtpPasswordProtected, @SmtpFromAddress, @SmtpToAddress, @RunInBackgroundWhenClosed)
            ON CONFLICT(id) DO UPDATE SET
                smoobu_api_key_protected = excluded.smoobu_api_key_protected,
                smoobu_api_secret_protected = excluded.smoobu_api_secret_protected,
                unifi_access_host = excluded.unifi_access_host,
                unifi_access_api_token_protected = excluded.unifi_access_api_token_protected,
                unifi_access_trust_any_ssl_cert = excluded.unifi_access_trust_any_ssl_cert,
                polling_interval_minutes = excluded.polling_interval_minutes,
                message_lead_days = excluded.message_lead_days,
                default_template_language = excluded.default_template_language,
                test_mode_enabled = excluded.test_mode_enabled,
                auto_approve_parsed_replies = excluded.auto_approve_parsed_replies,
                license_plate_country_prefixes_json = excluded.license_plate_country_prefixes_json,
                smtp_host = excluded.smtp_host,
                smtp_port = excluded.smtp_port,
                smtp_use_ssl = excluded.smtp_use_ssl,
                smtp_username = excluded.smtp_username,
                smtp_password_protected = excluded.smtp_password_protected,
                smtp_from_address = excluded.smtp_from_address,
                smtp_to_address = excluded.smtp_to_address,
                run_in_background_when_closed = excluded.run_in_background_when_closed;
            """,
            new
            {
                SmoobuApiKeyProtected = _protector.Protect(settings.SmoobuApiKey),
                SmoobuApiSecretProtected = _protector.Protect(settings.SmoobuApiSecret),
                settings.UnifiAccessHost,
                UnifiAccessApiTokenProtected = _protector.Protect(settings.UnifiAccessApiToken),
                settings.UnifiAccessTrustAnySslCert,
                settings.PollingIntervalMinutes,
                settings.MessageLeadDays,
                settings.DefaultTemplateLanguage,
                settings.TestModeEnabled,
                settings.AutoApproveParsedReplies,
                LicensePlateCountryPrefixesJson = JsonSerializer.Serialize(settings.LicensePlateCountryPrefixes),
                SmtpHost = settings.Smtp?.Host,
                SmtpPort = settings.Smtp?.Port,
                SmtpUseSsl = settings.Smtp?.UseSsl,
                SmtpUsername = settings.Smtp?.Username,
                SmtpPasswordProtected = _protector.Protect(settings.Smtp?.Password),
                SmtpFromAddress = settings.Smtp?.FromAddress,
                SmtpToAddress = settings.Smtp?.ToAddress,
                settings.RunInBackgroundWhenClosed,
            }).ConfigureAwait(false);
    }

    private sealed class SettingsRow
    {
        public byte[]? SmoobuApiKeyProtected { get; set; }
        public byte[]? SmoobuApiSecretProtected { get; set; }
        public string? UnifiAccessHost { get; set; }
        public byte[]? UnifiAccessApiTokenProtected { get; set; }
        public bool UnifiAccessTrustAnySslCert { get; set; }
        public int PollingIntervalMinutes { get; set; }
        public int MessageLeadDays { get; set; }
        public string DefaultTemplateLanguage { get; set; } = "en";
        public bool TestModeEnabled { get; set; }
        public bool AutoApproveParsedReplies { get; set; }
        public string? LicensePlateCountryPrefixesJson { get; set; }
        public string? SmtpHost { get; set; }
        public int? SmtpPort { get; set; }
        public bool? SmtpUseSsl { get; set; }
        public string? SmtpUsername { get; set; }
        public byte[]? SmtpPasswordProtected { get; set; }
        public string? SmtpFromAddress { get; set; }
        public string? SmtpToAddress { get; set; }
        public bool RunInBackgroundWhenClosed { get; set; } = true;
    }
}
