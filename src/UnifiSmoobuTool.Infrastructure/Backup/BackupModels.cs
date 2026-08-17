namespace UnifiSmoobuTool.Infrastructure.Backup;

internal sealed class BackupManifest
{
    public int SchemaVersion { get; set; } = 1;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public bool HasEncryptedSecrets { get; set; }
}

public sealed class BackupPreview
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public required bool HasEncryptedSecrets { get; init; }
    public required int TemplateCount { get; init; }
    public required int WebhookCount { get; init; }
    public required int ApartmentMappingCount { get; init; }
    public required int TestModeRuleCount { get; init; }
}

internal sealed class ProtectedSecrets
{
    public string? SmoobuApiKey { get; set; }
    public string? UnifiAccessApiToken { get; set; }
    public string? SmtpPassword { get; set; }
}
