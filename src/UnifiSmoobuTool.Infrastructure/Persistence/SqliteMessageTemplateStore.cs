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
            "SELECT language_code AS LanguageCode, body AS Body FROM message_templates ORDER BY language_code").ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task SaveAsync(MessageTemplate template, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO message_templates (language_code, body)
            VALUES (@LanguageCode, @Body)
            ON CONFLICT(language_code) DO UPDATE SET body = excluded.body;
            """,
            template).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string languageCode, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM message_templates WHERE language_code = @languageCode",
            new { languageCode }).ConfigureAwait(false);
    }
}
