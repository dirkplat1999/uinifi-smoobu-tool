using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Infrastructure.Persistence;
using UnifiSmoobuTool.Infrastructure.Security;
using Xunit;

namespace UnifiSmoobuTool.Infrastructure.Tests;

public class SqlitePersistenceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public SqlitePersistenceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"unifi-smoobu-test-{Guid.NewGuid():N}.db");
        _factory = new SqliteConnectionFactory(_dbPath);
        _factory.EnsureSchema();
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }
    }

    [Fact]
    public async Task ReservationStateStore_RoundTripsAllFields()
    {
        var store = new SqliteReservationStateStore(_factory);
        var state = new ReservationProcessingState
        {
            ReservationId = 42,
            RequestMessageSentAt = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero),
            GuestReplyReceivedAt = new DateTimeOffset(2026, 8, 17, 11, 0, 0, TimeSpan.Zero),
            ParsedLicensePlate = "AB123C",
            ParsedPinCode = "4821",
            NeedsManualReview = true,
            AccessCreatedAt = null,
            UnifiVisitorId = "visitor-1",
            AccessRevokedAt = null,
            ArrivalDayNotifiedAt = null,
        };

        await store.SaveAsync(state);
        var loaded = await store.GetAsync(42);

        Assert.NotNull(loaded);
        Assert.Equal(state.ReservationId, loaded!.ReservationId);
        Assert.Equal(state.RequestMessageSentAt, loaded.RequestMessageSentAt);
        Assert.Equal(state.ParsedLicensePlate, loaded.ParsedLicensePlate);
        Assert.True(loaded.NeedsManualReview);
        Assert.Equal("visitor-1", loaded.UnifiVisitorId);

        var pendingReview = await store.GetAllNeedingManualReviewAsync();
        Assert.Contains(pendingReview, s => s.ReservationId == 42);
    }

    [Fact]
    public async Task ReservationStateStore_Upsert_OverwritesExistingRow()
    {
        var store = new SqliteReservationStateStore(_factory);
        await store.SaveAsync(new ReservationProcessingState { ReservationId = 1, NeedsManualReview = true });
        await store.SaveAsync(new ReservationProcessingState { ReservationId = 1, NeedsManualReview = false, UnifiVisitorId = "v-99" });

        var loaded = await store.GetAsync(1);
        Assert.False(loaded!.NeedsManualReview);
        Assert.Equal("v-99", loaded.UnifiVisitorId);
    }

    [Fact]
    public async Task AppSettingsStore_RoundTripsAndProtectsSecrets()
    {
        var store = new SqliteAppSettingsStore(_factory, new SecretProtector());
        var settings = new AppSettings
        {
            SmoobuApiKey = "smoobu-secret",
            UnifiAccessHost = "https://192.168.1.1:12445",
            UnifiAccessApiToken = "unifi-secret",
            PollingIntervalMinutes = 15,
            LicensePlateCountryPrefixes = new List<string> { "NL", "D" },
            Smtp = new SmtpSettings { Host = "smtp.example.com", Username = "user", Password = "smtp-secret", FromAddress = "a@b.com", ToAddress = "c@d.com" },
        };

        await store.SaveAsync(settings);
        var loaded = await store.GetAsync();

        Assert.Equal("smoobu-secret", loaded.SmoobuApiKey);
        Assert.Equal("unifi-secret", loaded.UnifiAccessApiToken);
        Assert.Equal(15, loaded.PollingIntervalMinutes);
        Assert.Equal(new[] { "NL", "D" }, loaded.LicensePlateCountryPrefixes);
        Assert.NotNull(loaded.Smtp);
        Assert.Equal("smtp-secret", loaded.Smtp!.Password);

        // Raw DB column must not contain the plaintext secret.
        using var connection = _factory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT smoobu_api_key_protected FROM app_settings WHERE id = 1";
        var raw = (byte[])command.ExecuteScalar()!;
        var rawText = System.Text.Encoding.UTF8.GetString(raw);
        Assert.DoesNotContain("smoobu-secret", rawText);
    }

    [Fact]
    public async Task MessageTemplateStore_UpsertsByLanguage()
    {
        var store = new SqliteMessageTemplateStore(_factory);
        await store.SaveAsync(new MessageTemplate { LanguageCode = "en", Body = "Hello" });
        await store.SaveAsync(new MessageTemplate { LanguageCode = "en", Body = "Hi there" });
        await store.SaveAsync(new MessageTemplate { LanguageCode = "nl", Body = "Hallo" });

        var all = await store.GetAllAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal("Hi there", all.Single(t => t.LanguageCode == "en").Body);
    }

    [Fact]
    public async Task ApartmentMappingStore_RoundTripsUnifiResources()
    {
        var store = new SqliteApartmentMappingStore(_factory);
        var mapping = new ApartmentAccessMapping
        {
            SmoobuApartmentId = 7,
            ApartmentName = "Canal View",
            UnifiResources = { new UnifiResourceRef { Id = "door-1", Name = "Front Door", Type = "door" } },
        };

        await store.SaveAsync(mapping);
        var loaded = await store.GetAsync(7);

        Assert.NotNull(loaded);
        Assert.Single(loaded!.UnifiResources);
        Assert.Equal("Front Door", loaded.UnifiResources[0].Name);
    }

    [Fact]
    public async Task WebhookConfigStore_FiltersByApartmentAndTrigger()
    {
        var store = new SqliteWebhookConfigStore(_factory);
        var matching = new WebhookConfig
        {
            Id = Guid.NewGuid(),
            ApartmentId = 1,
            Name = "Chromecast greeting",
            Trigger = AutomationTrigger.AccessGranted,
            Method = WebhookMethod.Get,
            Url = "http://display.local/greet?name={{guest_first_name}}",
            Enabled = true,
        };
        var nonMatchingTrigger = matching with { Id = Guid.NewGuid(), Trigger = AutomationTrigger.ArrivalDay };
        var nonMatchingApartment = matching with { Id = Guid.NewGuid(), ApartmentId = 2 };
        var errorWebhook = new WebhookConfig
        {
            Id = Guid.NewGuid(),
            ApartmentId = null,
            Name = "Homey error alert",
            Trigger = AutomationTrigger.ErrorOccurred,
            Method = WebhookMethod.Post,
            Url = "http://homey.local/trigger",
            Enabled = true,
        };

        await store.SaveAsync(matching);
        await store.SaveAsync(nonMatchingTrigger);
        await store.SaveAsync(nonMatchingApartment);
        await store.SaveAsync(errorWebhook);

        var results = await store.GetForApartmentAsync(1, AutomationTrigger.AccessGranted);
        Assert.Single(results);
        Assert.Equal("Chromecast greeting", results[0].Name);

        var errorWebhooks = await store.GetErrorWebhooksAsync();
        Assert.Single(errorWebhooks);
        Assert.Equal("Homey error alert", errorWebhooks[0].Name);
    }

    [Fact]
    public async Task TestModeRuleStore_AddsAndDeletes()
    {
        var store = new SqliteTestModeRuleStore(_factory);
        var rule = new TestModeRule { Type = TestModeRuleType.Email, Value = "dirk@example.com" };

        await store.SaveAsync(rule);
        Assert.Single(await store.GetAllAsync());

        await store.DeleteAsync(rule);
        Assert.Empty(await store.GetAllAsync());
    }
}
