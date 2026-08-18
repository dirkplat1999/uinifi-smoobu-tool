using System.Globalization;
using Dapper;
using UnifiSmoobuTool.Core.Abstractions;
using UnifiSmoobuTool.Core.Models;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteReservationStateStore : IReservationStateStore
{
    private const string SelectColumns = """
        reservation_id AS ReservationId,
        request_message_sent_at AS RequestMessageSentAt,
        guest_reply_received_at AS GuestReplyReceivedAt,
        parsed_license_plate AS ParsedLicensePlate,
        parsed_pin_code AS ParsedPinCode,
        needs_manual_review AS NeedsManualReview,
        access_created_at AS AccessCreatedAt,
        unifi_visitor_id AS UnifiVisitorId,
        access_revoked_at AS AccessRevokedAt,
        arrival_day_notified_at AS ArrivalDayNotifiedAt,
        clarification_requested_at AS ClarificationRequestedAt,
        confirmation_sent_at AS ConfirmationSentAt
        """;

    private readonly SqliteConnectionFactory _factory;

    public SqliteReservationStateStore(SqliteConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<ReservationProcessingState?> GetAsync(long reservationId, CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var row = await connection.QuerySingleOrDefaultAsync<ReservationStateRow>(
            $"SELECT {SelectColumns} FROM reservation_state WHERE reservation_id = @reservationId",
            new { reservationId }).ConfigureAwait(false);
        return row?.ToModel();
    }

    public async Task SaveAsync(ReservationProcessingState state, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        using var connection = _factory.CreateOpenConnection();
        await connection.ExecuteAsync("""
            INSERT INTO reservation_state
                (reservation_id, request_message_sent_at, guest_reply_received_at, parsed_license_plate,
                 parsed_pin_code, needs_manual_review, access_created_at, unifi_visitor_id, access_revoked_at,
                 arrival_day_notified_at, clarification_requested_at, confirmation_sent_at)
            VALUES
                (@ReservationId, @RequestMessageSentAt, @GuestReplyReceivedAt, @ParsedLicensePlate,
                 @ParsedPinCode, @NeedsManualReview, @AccessCreatedAt, @UnifiVisitorId, @AccessRevokedAt,
                 @ArrivalDayNotifiedAt, @ClarificationRequestedAt, @ConfirmationSentAt)
            ON CONFLICT(reservation_id) DO UPDATE SET
                request_message_sent_at = excluded.request_message_sent_at,
                guest_reply_received_at = excluded.guest_reply_received_at,
                parsed_license_plate = excluded.parsed_license_plate,
                parsed_pin_code = excluded.parsed_pin_code,
                needs_manual_review = excluded.needs_manual_review,
                access_created_at = excluded.access_created_at,
                unifi_visitor_id = excluded.unifi_visitor_id,
                access_revoked_at = excluded.access_revoked_at,
                arrival_day_notified_at = excluded.arrival_day_notified_at,
                clarification_requested_at = excluded.clarification_requested_at,
                confirmation_sent_at = excluded.confirmation_sent_at;
            """,
            ReservationStateRow.FromModel(state)).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ReservationProcessingState>> GetAllNeedingManualReviewAsync(CancellationToken ct = default)
    {
        using var connection = _factory.CreateOpenConnection();
        var rows = await connection.QueryAsync<ReservationStateRow>(
            $"SELECT {SelectColumns} FROM reservation_state WHERE needs_manual_review = 1").ConfigureAwait(false);
        return rows.Select(r => r.ToModel()).ToList();
    }

    private sealed class ReservationStateRow
    {
        public long ReservationId { get; set; }
        public string? RequestMessageSentAt { get; set; }
        public string? GuestReplyReceivedAt { get; set; }
        public string? ParsedLicensePlate { get; set; }
        public string? ParsedPinCode { get; set; }
        public bool NeedsManualReview { get; set; }
        public string? AccessCreatedAt { get; set; }
        public string? UnifiVisitorId { get; set; }
        public string? AccessRevokedAt { get; set; }
        public string? ArrivalDayNotifiedAt { get; set; }
        public string? ClarificationRequestedAt { get; set; }
        public string? ConfirmationSentAt { get; set; }

        public ReservationProcessingState ToModel() => new()
        {
            ReservationId = ReservationId,
            RequestMessageSentAt = Parse(RequestMessageSentAt),
            GuestReplyReceivedAt = Parse(GuestReplyReceivedAt),
            ParsedLicensePlate = ParsedLicensePlate,
            ParsedPinCode = ParsedPinCode,
            NeedsManualReview = NeedsManualReview,
            AccessCreatedAt = Parse(AccessCreatedAt),
            UnifiVisitorId = UnifiVisitorId,
            AccessRevokedAt = Parse(AccessRevokedAt),
            ArrivalDayNotifiedAt = Parse(ArrivalDayNotifiedAt),
            ClarificationRequestedAt = Parse(ClarificationRequestedAt),
            ConfirmationSentAt = Parse(ConfirmationSentAt),
        };

        public static ReservationStateRow FromModel(ReservationProcessingState s) => new()
        {
            ReservationId = s.ReservationId,
            RequestMessageSentAt = Format(s.RequestMessageSentAt),
            GuestReplyReceivedAt = Format(s.GuestReplyReceivedAt),
            ParsedLicensePlate = s.ParsedLicensePlate,
            ParsedPinCode = s.ParsedPinCode,
            NeedsManualReview = s.NeedsManualReview,
            AccessCreatedAt = Format(s.AccessCreatedAt),
            UnifiVisitorId = s.UnifiVisitorId,
            AccessRevokedAt = Format(s.AccessRevokedAt),
            ArrivalDayNotifiedAt = Format(s.ArrivalDayNotifiedAt),
            ClarificationRequestedAt = Format(s.ClarificationRequestedAt),
            ConfirmationSentAt = Format(s.ConfirmationSentAt),
        };

        private static DateTimeOffset? Parse(string? value) =>
            string.IsNullOrEmpty(value) ? null : DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static string? Format(DateTimeOffset? value) => value?.ToString("O", CultureInfo.InvariantCulture);
    }
}
