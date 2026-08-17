using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteWebhookConfigStore : IWebhookConfigStore
{
    private const string SelectColumns = """
        id AS Id,
        apartment_id AS ApartmentId,
        name AS Name,
        trigger_event AS TriggerRaw,
        method AS MethodRaw,
        url AS Url,
        payload_template AS PayloadTemplate,
        enabled AS Enabled
        """;

    private readonly SqliteConnectionFactory _factory;

    public SqliteWebhookConfigStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<WebhookConfig>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<WebhookRow>($"SELECT {SelectColumns} FROM webhook_configs ORDER BY name").ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<WebhookConfig>> GetForApartmentAsync(int apartmentId, AutomationTrigger trigger, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<WebhookRow>(
            $"SELECT {SelectColumns} FROM webhook_configs WHERE apartment_id = @apartmentId AND trigger_event = @triggerEvent AND enabled = 1",
            new { apartmentId, triggerEvent = trigger.ToString() }).ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task<IReadOnlyList<WebhookConfig>> GetErrorWebhooksAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<WebhookRow>(
            $"SELECT {SelectColumns} FROM webhook_configs WHERE apartment_id IS NULL AND trigger_event = @triggerEvent AND enabled = 1",
            new { triggerEvent = AutomationTrigger.ErrorOccurred.ToString() }).ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task SaveAsync(WebhookConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO webhook_configs (id, apartment_id, name, trigger_event, method, url, payload_template, enabled)
            VALUES (@Id, @ApartmentId, @Name, @TriggerEvent, @Method, @Url, @PayloadTemplate, @Enabled)
            ON CONFLICT(id) DO UPDATE SET
                apartment_id = excluded.apartment_id,
                name = excluded.name,
                trigger_event = excluded.trigger_event,
                method = excluded.method,
                url = excluded.url,
                payload_template = excluded.payload_template,
                enabled = excluded.enabled;
            """,
            new
            {
                Id = config.Id.ToString(),
                config.ApartmentId,
                config.Name,
                TriggerEvent = config.Trigger.ToString(),
                Method = config.Method.ToString(),
                config.Url,
                config.PayloadTemplate,
                config.Enabled,
            }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("DELETE FROM webhook_configs WHERE id = @id", new { id = id.ToString() }).ConfigureAwait(false);
    }

    private static WebhookConfig ToModel(WebhookRow row) => new()
    {
        Id = Guid.Parse(row.Id),
        ApartmentId = row.ApartmentId,
        Name = row.Name,
        Trigger = Enum.Parse<AutomationTrigger>(row.TriggerRaw),
        Method = Enum.Parse<WebhookMethod>(row.MethodRaw),
        Url = row.Url,
        PayloadTemplate = row.PayloadTemplate,
        Enabled = row.Enabled,
    };

    private sealed class WebhookRow
    {
        public string Id { get; set; } = "";
        public int? ApartmentId { get; set; }
        public string Name { get; set; } = "";
        public string TriggerRaw { get; set; } = "";
        public string MethodRaw { get; set; } = "";
        public string Url { get; set; } = "";
        public string? PayloadTemplate { get; set; }
        public bool Enabled { get; set; }
    }
}
