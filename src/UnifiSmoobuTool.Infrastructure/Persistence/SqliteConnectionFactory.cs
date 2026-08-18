using Microsoft.Data.Sqlite;

namespace UnifiSmoobuTool.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? databasePath = null)
    {
        var path = databasePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UnifiSmoobuTool", "app.db");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
    }

    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    public void EnsureSchema()
    {
        using var connection = CreateOpenConnection();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = SchemaSql;
            command.ExecuteNonQuery();
        }

        // CREATE TABLE IF NOT EXISTS above only applies to brand-new databases; existing
        // databases from earlier versions need columns added explicitly.
        AddColumnIfMissing(connection, "app_settings", "smoobu_api_secret_protected", "BLOB NULL");
    }

    private static void AddColumnIfMissing(SqliteConnection connection, string table, string column, string columnDefinition)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = @column";
            checkCommand.Parameters.AddWithValue("@column", column);
            var exists = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;
            if (exists)
            {
                return;
            }
        }

        using var alterCommand = connection.CreateCommand();
        alterCommand.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {columnDefinition}";
        alterCommand.ExecuteNonQuery();
    }

    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS reservation_state (
            reservation_id INTEGER PRIMARY KEY,
            request_message_sent_at TEXT NULL,
            guest_reply_received_at TEXT NULL,
            parsed_license_plate TEXT NULL,
            parsed_pin_code TEXT NULL,
            needs_manual_review INTEGER NOT NULL DEFAULT 0,
            access_created_at TEXT NULL,
            unifi_visitor_id TEXT NULL,
            access_revoked_at TEXT NULL,
            arrival_day_notified_at TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS app_settings (
            id INTEGER PRIMARY KEY CHECK (id = 1),
            smoobu_api_key_protected BLOB NULL,
            smoobu_api_secret_protected BLOB NULL,
            unifi_access_host TEXT NULL,
            unifi_access_api_token_protected BLOB NULL,
            unifi_access_trust_any_ssl_cert INTEGER NOT NULL DEFAULT 1,
            polling_interval_minutes INTEGER NOT NULL DEFAULT 10,
            message_lead_days INTEGER NOT NULL DEFAULT 3,
            default_template_language TEXT NOT NULL DEFAULT 'en',
            test_mode_enabled INTEGER NOT NULL DEFAULT 0,
            auto_approve_parsed_replies INTEGER NOT NULL DEFAULT 1,
            license_plate_country_prefixes_json TEXT NOT NULL DEFAULT '[]',
            smtp_host TEXT NULL,
            smtp_port INTEGER NULL,
            smtp_use_ssl INTEGER NULL,
            smtp_username TEXT NULL,
            smtp_password_protected BLOB NULL,
            smtp_from_address TEXT NULL,
            smtp_to_address TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS message_templates (
            language_code TEXT PRIMARY KEY,
            body TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS apartment_mappings (
            apartment_id INTEGER PRIMARY KEY,
            apartment_name TEXT NOT NULL,
            unifi_resources_json TEXT NOT NULL DEFAULT '[]'
        );

        CREATE TABLE IF NOT EXISTS webhook_configs (
            id TEXT PRIMARY KEY,
            apartment_id INTEGER NULL,
            name TEXT NOT NULL,
            trigger_event TEXT NOT NULL,
            method TEXT NOT NULL,
            url TEXT NOT NULL,
            payload_template TEXT NULL,
            enabled INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS test_mode_rules (
            type TEXT NOT NULL,
            value TEXT NOT NULL,
            PRIMARY KEY (type, value)
        );
        """;
}
