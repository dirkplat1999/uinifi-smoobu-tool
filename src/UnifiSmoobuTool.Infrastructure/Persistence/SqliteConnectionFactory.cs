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
        AddColumnIfMissing(connection, "app_settings", "run_in_background_when_closed", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "app_settings", "guest_messaging_enabled", "INTEGER NOT NULL DEFAULT 1");
        AddColumnIfMissing(connection, "reservation_state", "clarification_requested_at", "TEXT NULL");
        AddColumnIfMissing(connection, "reservation_state", "confirmation_sent_at", "TEXT NULL");
        AddColumnIfMissing(connection, "reservation_state", "messaging_override_enabled", "INTEGER NOT NULL DEFAULT 0");
        MigrateMessageTemplatesTable(connection);
        AddColumnIfMissing(connection, "message_templates", "subject", "TEXT NULL");
    }

    /// <summary>Seeds Request/Clarification/Confirmation templates for English, Dutch, German, and
    /// French - but only when the table is completely empty, so a user's own templates (including
    /// a partial or single-language set) are never overwritten. Not part of <see cref="EnsureSchema"/>
    /// so tests that only need the schema (not seed data) stay unaffected; the app calls this once
    /// explicitly at startup, right after <see cref="EnsureSchema"/>.</summary>
    public void SeedDefaultTemplatesIfEmpty()
    {
        using var connection = CreateOpenConnection();
        SeedDefaultTemplatesIfEmpty(connection);
    }

    /// <summary>The original schema keyed message_templates on language_code alone (one template
    /// per language, request-only). Adding <see cref="Models.MessageTemplateKind"/> needs a
    /// composite key, which SQLite can't add via ALTER TABLE - so existing databases get their
    /// table recreated, with every existing row preserved as a "Request" template.</summary>
    private static void MigrateMessageTemplatesTable(SqliteConnection connection)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('message_templates') WHERE name = 'kind'";
            var hasKind = Convert.ToInt64(checkCommand.ExecuteScalar()) > 0;
            if (hasKind)
            {
                return;
            }
        }

        using var migrateCommand = connection.CreateCommand();
        migrateCommand.CommandText = """
            ALTER TABLE message_templates RENAME TO message_templates_old;

            CREATE TABLE message_templates (
                language_code TEXT NOT NULL,
                kind TEXT NOT NULL DEFAULT 'Request',
                body TEXT NOT NULL,
                PRIMARY KEY (language_code, kind)
            );

            INSERT INTO message_templates (language_code, kind, body)
                SELECT language_code, 'Request', body FROM message_templates_old;

            DROP TABLE message_templates_old;
            """;
        migrateCommand.ExecuteNonQuery();
    }

    private static void SeedDefaultTemplatesIfEmpty(SqliteConnection connection)
    {
        using (var checkCommand = connection.CreateCommand())
        {
            checkCommand.CommandText = "SELECT COUNT(*) FROM message_templates";
            var count = Convert.ToInt64(checkCommand.ExecuteScalar());
            if (count > 0)
            {
                return;
            }
        }

        using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = "INSERT INTO message_templates (language_code, kind, subject, body) VALUES (@lang, @kind, @subject, @body)";
        var langParam = insertCommand.CreateParameter();
        langParam.ParameterName = "@lang";
        insertCommand.Parameters.Add(langParam);
        var kindParam = insertCommand.CreateParameter();
        kindParam.ParameterName = "@kind";
        insertCommand.Parameters.Add(kindParam);
        var subjectParam = insertCommand.CreateParameter();
        subjectParam.ParameterName = "@subject";
        insertCommand.Parameters.Add(subjectParam);
        var bodyParam = insertCommand.CreateParameter();
        bodyParam.ParameterName = "@body";
        insertCommand.Parameters.Add(bodyParam);

        foreach (var template in Core.Services.DefaultMessageTemplates.All)
        {
            langParam.Value = template.LanguageCode;
            kindParam.Value = template.Kind.ToString();
            subjectParam.Value = (object?)template.Subject ?? DBNull.Value;
            bodyParam.Value = template.Body;
            insertCommand.ExecuteNonQuery();
        }
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
            arrival_day_notified_at TEXT NULL,
            clarification_requested_at TEXT NULL,
            confirmation_sent_at TEXT NULL
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
            smtp_to_address TEXT NULL,
            run_in_background_when_closed INTEGER NOT NULL DEFAULT 1,
            guest_messaging_enabled INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS message_templates (
            language_code TEXT NOT NULL,
            kind TEXT NOT NULL DEFAULT 'Request',
            subject TEXT NULL,
            body TEXT NOT NULL,
            PRIMARY KEY (language_code, kind)
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

        CREATE TABLE IF NOT EXISTS channel_messaging_settings (
            channel_name TEXT PRIMARY KEY,
            enabled INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS manual_bookings (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            apartment_id INTEGER NOT NULL,
            apartment_name TEXT NOT NULL,
            guest_first_name TEXT NOT NULL,
            guest_last_name TEXT NOT NULL,
            guest_email TEXT NOT NULL,
            guest_language TEXT NULL,
            arrival TEXT NOT NULL,
            departure TEXT NOT NULL,
            cancelled INTEGER NOT NULL DEFAULT 0
        );
        """;
}
