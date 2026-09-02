using System.Globalization;
using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteManualBookingStore : IManualBookingStore
{
    private const string SelectColumns = """
        id AS Id,
        apartment_id AS ApartmentId,
        apartment_name AS ApartmentName,
        guest_first_name AS GuestFirstName,
        guest_last_name AS GuestLastName,
        guest_email AS GuestEmail,
        guest_language AS GuestLanguage,
        arrival AS ArrivalRaw,
        departure AS DepartureRaw,
        cancelled AS Cancelled
        """;

    private readonly SqliteConnectionFactory _factory;

    public SqliteManualBookingStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<IReadOnlyList<ManualBooking>> GetAllAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<ManualBookingRow>(
            $"SELECT {SelectColumns} FROM manual_bookings ORDER BY arrival").ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    public async Task<ManualBooking?> GetAsync(long id, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ManualBookingRow>(
            $"SELECT {SelectColumns} FROM manual_bookings WHERE id = @id", new { id }).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task<long> AddAsync(ManualBooking booking, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(booking);
        using var connection = _factory.CreateOpenConnection();
        var id = await connection.ExecuteScalarAsync<long>("""
            INSERT INTO manual_bookings
                (apartment_id, apartment_name, guest_first_name, guest_last_name, guest_email,
                 guest_language, arrival, departure, cancelled)
            VALUES
                (@ApartmentId, @ApartmentName, @GuestFirstName, @GuestLastName, @GuestEmail,
                 @GuestLanguage, @Arrival, @Departure, @Cancelled);
            SELECT last_insert_rowid();
            """,
            ManualBookingRow.FromModel(booking)).ConfigureAwait(false);
        return id;
    }

    public async Task UpdateAsync(ManualBooking booking, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(booking);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            UPDATE manual_bookings SET
                apartment_id = @ApartmentId,
                apartment_name = @ApartmentName,
                guest_first_name = @GuestFirstName,
                guest_last_name = @GuestLastName,
                guest_email = @GuestEmail,
                guest_language = @GuestLanguage,
                arrival = @Arrival,
                departure = @Departure,
                cancelled = @Cancelled
            WHERE id = @Id;
            """,
            ManualBookingRow.FromModel(booking)).ConfigureAwait(false);
    }

    public async Task SetCancelledAsync(long id, bool cancelled, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE manual_bookings SET cancelled = @cancelled WHERE id = @id",
            new { id, cancelled }).ConfigureAwait(false);
    }

    private sealed class ManualBookingRow
    {
        public long Id { get; set; }
        public int ApartmentId { get; set; }
        public string ApartmentName { get; set; } = "";
        public string GuestFirstName { get; set; } = "";
        public string GuestLastName { get; set; } = "";
        public string GuestEmail { get; set; } = "";
        public string? GuestLanguage { get; set; }
        public string ArrivalRaw { get; set; } = "";
        public string DepartureRaw { get; set; } = "";
        public bool Cancelled { get; set; }

        public ManualBooking ToModel() => new()
        {
            Id = Id,
            ApartmentId = ApartmentId,
            ApartmentName = ApartmentName,
            GuestFirstName = GuestFirstName,
            GuestLastName = GuestLastName,
            GuestEmail = GuestEmail,
            GuestLanguage = GuestLanguage,
            Arrival = DateOnly.Parse(ArrivalRaw, CultureInfo.InvariantCulture),
            Departure = DateOnly.Parse(DepartureRaw, CultureInfo.InvariantCulture),
            Cancelled = Cancelled,
        };

        public static object FromModel(ManualBooking b) => new
        {
            b.Id,
            b.ApartmentId,
            b.ApartmentName,
            b.GuestFirstName,
            b.GuestLastName,
            b.GuestEmail,
            b.GuestLanguage,
            Arrival = b.Arrival.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Departure = b.Departure.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            b.Cancelled,
        };
    }
}
