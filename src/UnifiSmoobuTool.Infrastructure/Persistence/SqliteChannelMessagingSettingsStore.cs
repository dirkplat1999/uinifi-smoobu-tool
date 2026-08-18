using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteChannelMessagingSettingsStore : IChannelMessagingSettingsStore
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteChannelMessagingSettingsStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<ChannelMessagingSetting>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<ChannelMessagingSetting>(
            "SELECT channel_name AS ChannelName, enabled AS Enabled FROM channel_messaging_settings ORDER BY channel_name").ConfigureAwait(false);
        return rows.ToList();
    }

    public async Task<ChannelMessagingSetting?> GetAsync(string channelName, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        return await connection.QuerySingleOrDefaultAsync<ChannelMessagingSetting>(
            "SELECT channel_name AS ChannelName, enabled AS Enabled FROM channel_messaging_settings WHERE channel_name = @channelName",
            new { channelName }).ConfigureAwait(false);
    }

    public async Task SaveAsync(ChannelMessagingSetting setting, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(setting);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO channel_messaging_settings (channel_name, enabled)
            VALUES (@ChannelName, @Enabled)
            ON CONFLICT(channel_name) DO UPDATE SET enabled = excluded.enabled;
            """,
            setting).ConfigureAwait(false);
    }

    public async Task EnsureRegisteredAsync(string channelName, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO channel_messaging_settings (channel_name, enabled)
            VALUES (@channelName, 1)
            ON CONFLICT(channel_name) DO NOTHING;
            """,
            new { channelName }).ConfigureAwait(false);
    }
}
