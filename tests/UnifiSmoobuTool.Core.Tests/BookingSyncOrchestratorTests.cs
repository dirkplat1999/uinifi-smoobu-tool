using Microsoft.Extensions.Logging.Abstractions;
using UnifiSmoobuTool.Core.Models;
using UnifiSmoobuTool.Core.Services;
using Xunit;

namespace UnifiSmoobuTool.Core.Tests;

public class BookingSyncOrchestratorTests
{
    private sealed class Harness
    {
        public FakeClock Clock { get; } = new() { UtcNow = new DateTimeOffset(2026, 8, 17, 10, 0, 0, TimeSpan.Zero) };
        public FakeSmoobuClient Smoobu { get; } = new();
        public FakeUnifiAccessClient Unifi { get; } = new();
        public InMemoryReservationStateStore States { get; } = new();
        public InMemoryAppSettingsStore Settings { get; } = new();
        public InMemoryMessageTemplateStore Templates { get; } = new();
        public InMemoryApartmentMappingStore Mappings { get; } = new();
        public InMemoryWebhookConfigStore Webhooks { get; } = new();
        public InMemoryTestModeRuleStore TestModeRules { get; } = new();
        public FakeWebhookSender WebhookSender { get; } = new();
        public FakeErrorNotifier ErrorNotifier { get; } = new();

        public Harness()
        {
            Settings.Settings.SmoobuApiKey = "key";
            Settings.Settings.UnifiAccessHost = "https://192.168.1.1:12445";
            Settings.Settings.UnifiAccessApiToken = "token";
            Templates.Templates.Add(new MessageTemplate
            {
                LanguageCode = "en",
                Kind = MessageTemplateKind.Request,
                Body = "Hi {{guest_first_name}}, please reply with your license plate and a 4-digit PIN.",
            });
            Templates.Templates.Add(new MessageTemplate
            {
                LanguageCode = "en",
                Kind = MessageTemplateKind.Clarification,
                Body = "Hi {{guest_first_name}}, we couldn't read that - could you resend your plate and PIN clearly?",
            });
            Templates.Templates.Add(new MessageTemplate
            {
                LanguageCode = "en",
                Kind = MessageTemplateKind.Confirmation,
                Body = "Thanks {{guest_first_name}}, got it!",
            });
        }

        public BookingSyncOrchestrator BuildOrchestrator() => new(
            Smoobu, Unifi, States, Settings, Templates, Mappings, Webhooks, TestModeRules,
            new WebhookDispatcher(WebhookSender), ErrorNotifier, Clock,
            NullLogger<BookingSyncOrchestrator>.Instance,
            TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"));
    }

    private static Reservation MakeReservation(long id, DateOnly arrival, DateOnly departure, int apartmentId = 1) => new()
    {
        Id = id,
        ApartmentId = apartmentId,
        ApartmentName = "Canal View",
        GuestFirstName = "Alex",
        GuestLastName = "Doe",
        GuestEmail = "alex@example.com",
        GuestPhone = "+31612345678",
        GuestLanguage = "en",
        Arrival = arrival,
        Departure = departure,
        Status = ReservationStatus.Confirmed,
    };

    [Fact]
    public async Task RunOnceAsync_SendsRequestMessage_WhenArrivalIsWithinLeadWindow()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        h.Smoobu.Reservations.Add(MakeReservation(1, today.AddDays(3), today.AddDays(6)));

        await h.BuildOrchestrator().RunOnceAsync();

        var sent = Assert.Single(h.Smoobu.SentMessages);
        Assert.Equal(1, sent.ReservationId);
        Assert.Contains("Alex", sent.Message);

        var state = await h.States.GetAsync(1);
        Assert.NotNull(state!.RequestMessageSentAt);
    }

    [Fact]
    public async Task RunOnceAsync_DoesNotSendMessage_WhenArrivalIsBeyondLeadWindow()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        h.Smoobu.Reservations.Add(MakeReservation(1, today.AddDays(10), today.AddDays(13)));

        await h.BuildOrchestrator().RunOnceAsync();

        Assert.Empty(h.Smoobu.SentMessages);
    }

    [Fact]
    public async Task RunOnceAsync_ProvisionsUnifiAccess_AfterConfidentGuestReply()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        var arrival = today.AddDays(3);
        var departure = today.AddDays(6);
        h.Smoobu.Reservations.Add(MakeReservation(1, arrival, departure));
        h.Mappings.Mappings.Add(new ApartmentAccessMapping
        {
            SmoobuApartmentId = 1,
            ApartmentName = "Canal View",
            UnifiResources = { new UnifiResourceRef { Id = "door-1", Name = "Front Door", Type = "door" } },
        });

        var orchestrator = h.BuildOrchestrator();
        await orchestrator.RunOnceAsync();

        h.Smoobu.Messages.Add(new GuestMessage
        {
            ReservationId = 1,
            Text = "Our plate is AB-123-C and the PIN is 4821",
            SentAt = h.Clock.UtcNow.AddMinutes(5),
            Direction = MessageDirection.GuestToHost,
        });
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(10);

        await orchestrator.RunOnceAsync();

        var visitor = Assert.Single(h.Unifi.Visitors);
        Assert.Equal("Alex", visitor.Request.FirstName);
        Assert.Equal("Doe", visitor.Request.LastName);
        Assert.Equal("4821", visitor.PinCode);
        Assert.Equal(new[] { "AB123C" }, visitor.LicensePlates);
        Assert.Equal(1, visitor.Request.StartTime.Hour);
        Assert.Single(visitor.Request.Resources);

        var state = await h.States.GetAsync(1);
        Assert.NotNull(state!.AccessCreatedAt);
        Assert.False(state.NeedsManualReview);

        Assert.Equal(2, h.Smoobu.SentMessages.Count);
        Assert.Contains(h.Smoobu.SentMessages, m => m.Message.Contains("Thanks", StringComparison.Ordinal));
        Assert.NotNull(state.ConfirmationSentAt);
    }

    [Fact]
    public async Task RunOnceAsync_FlagsManualReview_WhenReplyIsAmbiguous()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        h.Smoobu.Reservations.Add(MakeReservation(1, today.AddDays(3), today.AddDays(6)));

        var orchestrator = h.BuildOrchestrator();
        await orchestrator.RunOnceAsync();

        h.Smoobu.Messages.Add(new GuestMessage
        {
            ReservationId = 1,
            Text = "we'll send it later",
            SentAt = h.Clock.UtcNow.AddMinutes(5),
            Direction = MessageDirection.GuestToHost,
        });
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(10);

        await orchestrator.RunOnceAsync();

        Assert.Empty(h.Unifi.Visitors);
        var state = await h.States.GetAsync(1);
        Assert.True(state!.NeedsManualReview);

        Assert.Equal(2, h.Smoobu.SentMessages.Count);
        Assert.Contains(h.Smoobu.SentMessages, m => m.Message.Contains("couldn't read", StringComparison.Ordinal));
        Assert.NotNull(state.ClarificationRequestedAt);
    }

    [Fact]
    public async Task RunOnceAsync_ResolvesAfterClarification_WhenFollowUpReplyIsClear()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        var arrival = today.AddDays(3);
        var departure = today.AddDays(6);
        h.Smoobu.Reservations.Add(MakeReservation(1, arrival, departure));
        h.Mappings.Mappings.Add(new ApartmentAccessMapping { SmoobuApartmentId = 1, ApartmentName = "Canal View" });

        var orchestrator = h.BuildOrchestrator();
        await orchestrator.RunOnceAsync();

        h.Smoobu.Messages.Add(new GuestMessage
        {
            ReservationId = 1,
            Text = "we'll send it later",
            SentAt = h.Clock.UtcNow.AddMinutes(5),
            Direction = MessageDirection.GuestToHost,
        });
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(10);
        await orchestrator.RunOnceAsync();

        var stateAfterClarification = await h.States.GetAsync(1);
        Assert.True(stateAfterClarification!.NeedsManualReview);
        Assert.NotNull(stateAfterClarification.ClarificationRequestedAt);

        h.Smoobu.Messages.Add(new GuestMessage
        {
            ReservationId = 1,
            Text = "Sorry! Plate AB-123-C, PIN 4821",
            SentAt = h.Clock.UtcNow.AddMinutes(5),
            Direction = MessageDirection.GuestToHost,
        });
        h.Clock.UtcNow = h.Clock.UtcNow.AddMinutes(10);
        await orchestrator.RunOnceAsync();

        var visitor = Assert.Single(h.Unifi.Visitors);
        Assert.Equal("4821", visitor.PinCode);
        var state = await h.States.GetAsync(1);
        Assert.False(state!.NeedsManualReview);
        Assert.NotNull(state.AccessCreatedAt);
        Assert.NotNull(state.ConfirmationSentAt);

        // Request, Clarification, Confirmation - no duplicate clarifications sent.
        Assert.Equal(3, h.Smoobu.SentMessages.Count);
    }

    [Fact]
    public async Task ApproveManualReviewAsync_ProvisionsAccess_WithCorrectedValues()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        var arrival = today.AddDays(3);
        var departure = today.AddDays(6);
        h.Smoobu.Reservations.Add(MakeReservation(1, arrival, departure));

        var orchestrator = h.BuildOrchestrator();
        await orchestrator.RunOnceAsync();

        await orchestrator.ApproveManualReviewAsync(1, "NL-AB-12-CD", "1234");

        var visitor = Assert.Single(h.Unifi.Visitors);
        Assert.Equal("1234", visitor.PinCode);
        Assert.Equal(new[] { "AB12CD" }, visitor.LicensePlates);
    }

    [Fact]
    public async Task RunOnceAsync_RevokesAccess_WhenReservationIsCancelledAfterProvisioning()
    {
        var h = new Harness();
        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        var arrival = today.AddDays(3);
        var departure = today.AddDays(6);
        h.Smoobu.Reservations.Add(MakeReservation(1, arrival, departure));

        var orchestrator = h.BuildOrchestrator();
        await orchestrator.RunOnceAsync();
        await orchestrator.ApproveManualReviewAsync(1, "AB123C", "4821");

        var visitorId = h.Unifi.Visitors.Single().Id;
        Assert.False(h.Unifi.Visitors.Single().Deleted);

        h.Smoobu.Reservations[0] = h.Smoobu.Reservations[0] with { Status = ReservationStatus.Cancelled };
        await orchestrator.RunOnceAsync();

        Assert.True(h.Unifi.Visitors.Single(v => v.Id == visitorId).Deleted);
        var state = await h.States.GetAsync(1);
        Assert.NotNull(state!.AccessRevokedAt);
    }

    [Fact]
    public async Task RunOnceAsync_SkipsReservation_WhenTestModeEnabledAndNoRuleMatches()
    {
        var h = new Harness();
        h.Settings.Settings.TestModeEnabled = true;
        h.TestModeRules.Rules.Add(new TestModeRule { Type = TestModeRuleType.Email, Value = "someone-else@example.com" });

        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        h.Smoobu.Reservations.Add(MakeReservation(1, today.AddDays(3), today.AddDays(6)));

        await h.BuildOrchestrator().RunOnceAsync();

        Assert.Empty(h.Smoobu.SentMessages);
    }

    [Fact]
    public async Task RunOnceAsync_DoesNothing_WhenApiKeysAreNotConfigured()
    {
        var h = new Harness();
        h.Settings.Settings.SmoobuApiKey = null;

        var today = DateOnly.FromDateTime(h.Clock.UtcNow.UtcDateTime);
        h.Smoobu.Reservations.Add(MakeReservation(1, today.AddDays(3), today.AddDays(6)));

        await h.BuildOrchestrator().RunOnceAsync();

        Assert.Empty(h.Smoobu.SentMessages);
    }
}
