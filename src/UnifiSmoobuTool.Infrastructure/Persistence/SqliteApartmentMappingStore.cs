using System.Text.Json;
using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteApartmentMappingStore : IApartmentMappingStore
{
    private readonly SqliteConnectionFactory _factory;

    public SqliteApartmentMappingStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<ApartmentAccessMapping>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<MappingRow>(
            "SELECT apartment_id AS ApartmentId, apartment_name AS ApartmentName, unifi_resources_json AS UnifiResourcesJson FROM apartment_mappings ORDER BY apartment_name")
            .ConfigureAwait(false);
        return rows.Select(ToModel).ToList();
    }

    public async Task<ApartmentAccessMapping?> GetAsync(int apartmentId, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<MappingRow>(
            "SELECT apartment_id AS ApartmentId, apartment_name AS ApartmentName, unifi_resources_json AS UnifiResourcesJson FROM apartment_mappings WHERE apartment_id = @apartmentId",
            new { apartmentId }).ConfigureAwait(false);
        return row is null ? null : ToModel(row);
    }

    public async Task SaveAsync(ApartmentAccessMapping mapping, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO apartment_mappings (apartment_id, apartment_name, unifi_resources_json)
            VALUES (@ApartmentId, @ApartmentName, @UnifiResourcesJson)
            ON CONFLICT(apartment_id) DO UPDATE SET
                apartment_name = excluded.apartment_name,
                unifi_resources_json = excluded.unifi_resources_json;
            """,
            new
            {
                ApartmentId = mapping.SmoobuApartmentId,
                mapping.ApartmentName,
                UnifiResourcesJson = JsonSerializer.Serialize(mapping.UnifiResources),
            }).ConfigureAwait(false);
    }

    private static ApartmentAccessMapping ToModel(MappingRow row) => new()
    {
        SmoobuApartmentId = row.ApartmentId,
        ApartmentName = row.ApartmentName,
        UnifiResources = string.IsNullOrWhiteSpace(row.UnifiResourcesJson)
            ? new List<UnifiResourceRef>()
            : JsonSerializer.Deserialize<List<UnifiResourceRef>>(row.UnifiResourcesJson) ?? new List<UnifiResourceRef>(),
    };

    private sealed class MappingRow
    {
        public int ApartmentId { get; set; }
        public string ApartmentName { get; set; } = "";
        public string? UnifiResourcesJson { get; set; }
    }
}
