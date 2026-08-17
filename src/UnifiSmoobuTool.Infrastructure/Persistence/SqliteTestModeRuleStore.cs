using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteTestModeRuleStore : ITestModeRuleStore
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteTestModeRuleStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<TestModeRule>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<RuleRow>(
            "SELECT type AS TypeRaw, value AS Value FROM test_mode_rules ORDER BY type, value").ConfigureAwait(false);
        return rows.Select(r => new TestModeRule { Type = Enum.Parse<TestModeRuleType>(r.TypeRaw), Value = r.Value }).ToList();
    }

    public async Task SaveAsync(TestModeRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO test_mode_rules (type, value) VALUES (@Type, @Value)
            ON CONFLICT(type, value) DO NOTHING;
            """,
            new { Type = rule.Type.ToString(), rule.Value }).ConfigureAwait(false);
    }

    public async Task DeleteAsync(TestModeRule rule, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rule);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "DELETE FROM test_mode_rules WHERE type = @type AND value = @value",
            new { type = rule.Type.ToString(), value = rule.Value }).ConfigureAwait(false);
    }

    private sealed class RuleRow
    {
        public string TypeRaw { get; set; } = "";
        public string Value { get; set; } = "";
    }
}
