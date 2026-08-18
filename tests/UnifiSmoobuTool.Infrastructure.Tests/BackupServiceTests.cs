using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Infrastructure.Backup;
using UnifiSmoobuTool.Infrastructure.Persistence;
using UnifiSmoobuTool.Infrastructure.Security;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class BackupServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly string _zipPath;
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteAppSettingsStore _settingsStore;
    private readonly SqliteMessageTemplateStore _templateStore;
    private readonly SqliteWebhookConfigStore _webhookStore;
    private readonly SqliteApartmentMappingStore _mappingStore;
    private readonly SqliteTestModeRuleStore _testModeStore;
    private readonly SqliteChannelMessagingSettingsStore _channelSettingsStore;
    private readonly BackupService _backupService;

    public BackupServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"unifi-smoobu-backup-test-{Guid.NewGuid():N}.db");
        _zipPath = Path.Combine(Path.GetTempPath(), $"unifi-smoobu-backup-test-{Guid.NewGuid():N}.zip");
        _factory = new SqliteConnectionFactory(_dbPath);
        _factory.EnsureSchema();

        var protector = new SecretProtector();
        _settingsStore = new SqliteAppSettingsStore(_factory, protector);
        _templateStore = new SqliteMessageTemplateStore(_factory);
        _webhookStore = new SqliteWebhookConfigStore(_factory);
        _mappingStore = new SqliteApartmentMappingStore(_factory);
        _testModeStore = new SqliteTestModeRuleStore(_factory);
        _channelSettingsStore = new SqliteChannelMessagingSettingsStore(_factory);

        _backupService = new BackupService(_settingsStore, _templateStore, _webhookStore, _mappingStore, _testModeStore, _channelSettingsStore);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
        if (File.Exists(_zipPath)) File.Delete(_zipPath);
    }

    private async Task SeedAsync()
    {
        await _settingsStore.SaveAsync(new AppSettings
        {
            SmoobuApiKey = "smoobu-secret",
            UnifiAccessHost = "https://192.168.1.1:12445",
            UnifiAccessApiToken = "unifi-secret",
            PollingIntervalMinutes = 20,
        });
        await _templateStore.SaveAsync(new MessageTemplate { LanguageCode = "en", Body = "Hi {{guest_first_name}}" });
        await _templateStore.SaveAsync(new MessageTemplate { LanguageCode = "nl", Body = "Hoi {{guest_first_name}}" });
        await _mappingStore.SaveAsync(new ApartmentAccessMapping { SmoobuApartmentId = 1, ApartmentName = "Canal View" });
        await _webhookStore.SaveAsync(new WebhookConfig
        {
            Id = Guid.NewGuid(),
            ApartmentId = 1,
            Name = "Greeting",
            Trigger = AutomationTrigger.AccessGranted,
            Method = WebhookMethod.Get,
            Url = "http://display.local/greet",
        });
        await _testModeStore.SaveAsync(new TestModeRule { Type = TestModeRuleType.Email, Value = "dirk@example.com" });
        await _channelSettingsStore.SaveAsync(new ChannelMessagingSetting { ChannelName = "Airbnb", Enabled = false });
    }

    [Fact]
    public async Task ExportThenImport_WithoutCredentials_PreservesNonSecretConfig_ButNotSecrets()
    {
        await SeedAsync();

        await _backupService.ExportAsync(_zipPath, includeCredentials: false, passphrase: null);

        // Wipe local state, then restore from the backup into a fresh database.
        var freshDbPath = Path.Combine(Path.GetTempPath(), $"unifi-smoobu-restore-{Guid.NewGuid():N}.db");
        try
        {
            var freshFactory = new SqliteConnectionFactory(freshDbPath);
            freshFactory.EnsureSchema();
            var protector = new SecretProtector();
            var freshSettings = new SqliteAppSettingsStore(freshFactory, protector);
            var freshTemplates = new SqliteMessageTemplateStore(freshFactory);
            var freshWebhooks = new SqliteWebhookConfigStore(freshFactory);
            var freshMappings = new SqliteApartmentMappingStore(freshFactory);
            var freshTestMode = new SqliteTestModeRuleStore(freshFactory);
            var freshChannelSettings = new SqliteChannelMessagingSettingsStore(freshFactory);
            var freshBackupService = new BackupService(freshSettings, freshTemplates, freshWebhooks, freshMappings, freshTestMode, freshChannelSettings);

            await freshBackupService.ImportAsync(_zipPath, passphrase: null);

            var settings = await freshSettings.GetAsync();
            Assert.Null(settings.SmoobuApiKey);
            Assert.Null(settings.UnifiAccessApiToken);
            Assert.Equal("https://192.168.1.1:12445", settings.UnifiAccessHost);
            Assert.Equal(20, settings.PollingIntervalMinutes);

            Assert.Equal(2, (await freshTemplates.GetAllAsync()).Count);
            Assert.Single(await freshWebhooks.GetAllAsync());
            Assert.Single(await freshMappings.GetAllAsync());
            Assert.Single(await freshTestMode.GetAllAsync());
            Assert.Single(await freshChannelSettings.GetAllAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(freshDbPath)) File.Delete(freshDbPath);
        }
    }

    [Fact]
    public async Task ExportThenImport_WithCredentials_RecoversSecretsWithCorrectPassphrase()
    {
        await SeedAsync();

        await _backupService.ExportAsync(_zipPath, includeCredentials: true, passphrase: "correct-horse-battery-staple");

        var freshDbPath = Path.Combine(Path.GetTempPath(), $"unifi-smoobu-restore-{Guid.NewGuid():N}.db");
        try
        {
            var freshFactory = new SqliteConnectionFactory(freshDbPath);
            freshFactory.EnsureSchema();
            var freshSettings = new SqliteAppSettingsStore(freshFactory, new SecretProtector());
            var freshBackupService = new BackupService(
                freshSettings,
                new SqliteMessageTemplateStore(freshFactory),
                new SqliteWebhookConfigStore(freshFactory),
                new SqliteApartmentMappingStore(freshFactory),
                new SqliteTestModeRuleStore(freshFactory),
                new SqliteChannelMessagingSettingsStore(freshFactory));

            await freshBackupService.ImportAsync(_zipPath, passphrase: "correct-horse-battery-staple");

            var settings = await freshSettings.GetAsync();
            Assert.Equal("smoobu-secret", settings.SmoobuApiKey);
            Assert.Equal("unifi-secret", settings.UnifiAccessApiToken);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(freshDbPath)) File.Delete(freshDbPath);
        }
    }

    [Fact]
    public async Task Import_WithWrongPassphrase_Throws()
    {
        await SeedAsync();
        await _backupService.ExportAsync(_zipPath, includeCredentials: true, passphrase: "correct-horse-battery-staple");

        await Assert.ThrowsAnyAsync<System.Security.Cryptography.CryptographicException>(
            () => _backupService.ImportAsync(_zipPath, passphrase: "wrong-passphrase"));
    }

    [Fact]
    public async Task Export_WithCredentials_RequiresPassphrase()
    {
        await SeedAsync();
        await Assert.ThrowsAsync<ArgumentException>(
            () => _backupService.ExportAsync(_zipPath, includeCredentials: true, passphrase: null));
    }

    [Fact]
    public async Task PreviewImportAsync_ReportsCorrectCounts()
    {
        await SeedAsync();
        await _backupService.ExportAsync(_zipPath, includeCredentials: true, passphrase: "pw");

        var preview = await _backupService.PreviewImportAsync(_zipPath);

        Assert.Equal(2, preview.TemplateCount);
        Assert.Equal(1, preview.WebhookCount);
        Assert.Equal(1, preview.ApartmentMappingCount);
        Assert.Equal(1, preview.TestModeRuleCount);
        Assert.Equal(1, preview.ChannelSettingCount);
        Assert.True(preview.HasEncryptedSecrets);
    }
}
