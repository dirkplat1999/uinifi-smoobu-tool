using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Core.Tests;

internal sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
}

internal sealed class FakeSmoobuClient : ISmoobuClient
{
    public List<Apartment> Apartments { get; } = new();
    public List<Reservation> Reservations { get; } = new();
    public List<GuestMessage> Messages { get; } = new();
    public List<(long ReservationId, string Message)> SentMessages { get; } = new();

    public Task<IReadOnlyList<Apartment>> GetApartmentsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Apartment>>(Apartments);

    public Task<IReadOnlyList<Reservation>> GetReservationsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Reservation>>(
            Reservations.Where(r => r.Arrival <= to && r.Departure >= from).ToList());

    public Task<Reservation?> GetReservationAsync(long reservationId, CancellationToken ct = default)
        => Task.FromResult(Reservations.FirstOrDefault(r => r.Id == reservationId));

    public Task<IReadOnlyList<GuestMessage>> GetMessagesAsync(long reservationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<GuestMessage>>(
            Messages.Where(m => m.ReservationId == reservationId).ToList());

    public Task SendMessageToGuestAsync(long reservationId, string message, CancellationToken ct = default)
    {
        SentMessages.Add((reservationId, message));
        return Task.CompletedTask;
    }
}

internal sealed class FakeUnifiAccessClient : IUnifiAccessClient
{
    public sealed class FakeVisitor
    {
        public required string Id { get; init; }
        public required CreateVisitorRequest Request { get; init; }
        public string? PinCode { get; set; }
        public List<string> LicensePlates { get; } = new();
        public bool Deleted { get; set; }
    }

    public List<FakeVisitor> Visitors { get; } = new();
    private int _nextId = 1;

    public Task<IReadOnlyList<UnifiResourceRef>> GetDoorGroupTopologyAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UnifiResourceRef>>(Array.Empty<UnifiResourceRef>());

    public Task<string> CreateVisitorAsync(CreateVisitorRequest request, CancellationToken ct = default)
    {
        var id = (_nextId++).ToString();
        Visitors.Add(new FakeVisitor { Id = id, Request = request });
        return Task.FromResult(id);
    }

    public Task UpdateVisitorAsync(string visitorId, UpdateVisitorRequest request, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task DeleteVisitorAsync(string visitorId, bool force, CancellationToken ct = default)
    {
        var visitor = Visitors.FirstOrDefault(v => v.Id == visitorId);
        if (visitor is not null)
        {
            visitor.Deleted = true;
        }
        return Task.CompletedTask;
    }

    public Task AssignPinCodeAsync(string visitorId, string pinCode, CancellationToken ct = default)
    {
        Visitors.First(v => v.Id == visitorId).PinCode = pinCode;
        return Task.CompletedTask;
    }

    public Task AssignLicensePlatesAsync(string visitorId, IReadOnlyList<string> plates, CancellationToken ct = default)
    {
        Visitors.First(v => v.Id == visitorId).LicensePlates.AddRange(plates);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryReservationStateStore : IReservationStateStore
{
    private readonly Dictionary<long, ReservationProcessingState> _states = new();

    public Task<ReservationProcessingState?> GetAsync(long reservationId, CancellationToken ct = default)
        => Task.FromResult(_states.TryGetValue(reservationId, out var s) ? s : null);

    public Task SaveAsync(ReservationProcessingState state, CancellationToken ct = default)
    {
        _states[state.ReservationId] = state;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ReservationProcessingState>> GetAllNeedingManualReviewAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ReservationProcessingState>>(
            _states.Values.Where(s => s.NeedsManualReview).ToList());
}

internal sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    public AppSettings Settings { get; set; } = new();

    public Task<AppSettings> GetAsync(CancellationToken ct = default) => Task.FromResult(Settings);

    public Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryMessageTemplateStore : IMessageTemplateStore
{
    public List<MessageTemplate> Templates { get; } = new();

    public Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MessageTemplate>>(Templates);

    public Task SaveAsync(MessageTemplate template, CancellationToken ct = default)
    {
        Templates.RemoveAll(t => t.LanguageCode == template.LanguageCode && t.Kind == template.Kind);
        Templates.Add(template);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string languageCode, MessageTemplateKind kind, CancellationToken ct = default)
    {
        Templates.RemoveAll(t => t.LanguageCode == languageCode && t.Kind == kind);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryApartmentMappingStore : IApartmentMappingStore
{
    public List<ApartmentAccessMapping> Mappings { get; } = new();

    public Task<IReadOnlyList<ApartmentAccessMapping>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ApartmentAccessMapping>>(Mappings);

    public Task<ApartmentAccessMapping?> GetAsync(int apartmentId, CancellationToken ct = default)
        => Task.FromResult(Mappings.FirstOrDefault(m => m.SmoobuApartmentId == apartmentId));

    public Task SaveAsync(ApartmentAccessMapping mapping, CancellationToken ct = default)
    {
        Mappings.RemoveAll(m => m.SmoobuApartmentId == mapping.SmoobuApartmentId);
        Mappings.Add(mapping);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryWebhookConfigStore : IWebhookConfigStore
{
    public List<WebhookConfig> Configs { get; } = new();

    public Task<IReadOnlyList<WebhookConfig>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookConfig>>(Configs);

    public Task<IReadOnlyList<WebhookConfig>> GetForApartmentAsync(int apartmentId, AutomationTrigger trigger, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookConfig>>(
            Configs.Where(c => c.ApartmentId == apartmentId && c.Trigger == trigger).ToList());

    public Task<IReadOnlyList<WebhookConfig>> GetErrorWebhooksAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WebhookConfig>>(
            Configs.Where(c => c.ApartmentId == null && c.Trigger == AutomationTrigger.ErrorOccurred).ToList());

    public Task SaveAsync(WebhookConfig config, CancellationToken ct = default)
    {
        Configs.RemoveAll(c => c.Id == config.Id);
        Configs.Add(config);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Configs.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryTestModeRuleStore : ITestModeRuleStore
{
    public List<TestModeRule> Rules { get; } = new();

    public Task<IReadOnlyList<TestModeRule>> GetAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TestModeRule>>(Rules);

    public Task SaveAsync(TestModeRule rule, CancellationToken ct = default)
    {
        Rules.Add(rule);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(TestModeRule rule, CancellationToken ct = default)
    {
        Rules.RemoveAll(r => r.Type == rule.Type && r.Value == rule.Value);
        return Task.CompletedTask;
    }
}

internal sealed class FakeWebhookSender : IWebhookSender
{
    public List<(string Url, WebhookMethod Method, string? Payload)> Calls { get; } = new();

    public Task SendAsync(string url, WebhookMethod method, string? jsonOrFormPayload, CancellationToken ct = default)
    {
        Calls.Add((url, method, jsonOrFormPayload));
        return Task.CompletedTask;
    }
}

internal sealed class FakeErrorNotifier : IErrorNotifier
{
    public List<(string Component, string Message, Exception? Exception)> Calls { get; } = new();

    public Task NotifyErrorAsync(string component, string message, Exception? exception, CancellationToken ct = default)
    {
        Calls.Add((component, message, exception));
        return Task.CompletedTask;
    }
}
