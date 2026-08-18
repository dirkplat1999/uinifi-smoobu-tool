using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteMessageTemplateStore : IMessageTemplateStore
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteMessageTemplateStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<MessageTemplate>(
            "SELECT language_code AS LanguageCode, kind AS Kind, body AS Body FROM message_templates ORDER BY language_code, kind").ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SaveAsync(MessageTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO message_templates (language_code, kind, body)
            VALUES (@LanguageCode, @Kind, @Body)
            ON CONFLICT(language_code, kind) DO UPDATE SET body = excluded.body;
            """,
            new { template.LanguageCode, Kind = template.Kind.ToString(), template.Body }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string languageCode, MessageTemplateKind kind, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM message_templates WHERE language_code = @languageCode AND kind = @kind",
            new { languageCode, kind = kind.ToString() }).ConfigureAwait(false);
    }
}
