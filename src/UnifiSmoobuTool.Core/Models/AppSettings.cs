namespace UnifiSmoobuTool.Core.Models;

public sealed class AppSettings
{
    public string? SmoobuApiKey { get; set; }

    /// <summary>The HMAC signing secret paired with <see cref="SmoobuApiKey"/>. When present, the
    /// client signs every request (X-API-Key/X-Timestamp/X-Nonce/X-Signature). When absent, it
    /// falls back to Smoobu's legacy single "Api-Key" header scheme.</summary>
    public string? SmoobuApiSecret { get; set; }

    /// <summary>e.g. https://192.168.1.1:12445</summary>
    public string? UnifiAccessHost { get; set; }

    public string? UnifiAccessApiToken { get; set; }

    public bool UnifiAccessTrustAnySslCert { get; set; } = true;

    public int PollingIntervalMinutes { get; set; } = 10;

    public int MessageLeadDays { get; set; } = 3;

    public string DefaultTemplateLanguage { get; set; } = "en";

    public bool TestModeEnabled { get; set; }

    /// <summary>When false, confidently-parsed guest replies still wait for manual approval in the Dashboard.</summary>
    public bool AutoApproveParsedReplies { get; set; } = true;

    /// <summary>Leading country/land indicators to strip from license plate text (e.g. "NL", "D", "B", "F").</summary>
    public List<string> LicensePlateCountryPrefixes { get; set; } =
        new() { "NL", "D", "B", "F", "GB", "PL", "DK", "LUX", "A" };

    public SmtpSettings? Smtp { get; set; }

    /// <summary>When true (default), closing the main window minimizes it to the system tray
    /// instead of exiting, so the background sync loop keeps running. When false, closing the
    /// window exits the app entirely.</summary>
    public bool RunInBackgroundWhenClosed { get; set; } = true;
}

public sealed class SmtpSettings
{
    public required string Host { get; init; }
    public int Port { get; init; } = 587;
    public bool UseSsl { get; init; } = true;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public required string FromAddress { get; init; }
    public required string ToAddress { get; init; }
}
