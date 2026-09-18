using Attendance.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Data;

internal static class SchemaMigrator
{
    public static void Apply(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS roles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                hourly_rate TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1,
                rates_json TEXT
            );
            CREATE TABLE IF NOT EXISTS employees (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                role_id INTEGER NOT NULL,
                device_user_id TEXT NOT NULL UNIQUE,
                is_active INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (role_id) REFERENCES roles(id)
            );
            CREATE TABLE IF NOT EXISTS punches (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                employee_id INTEGER NOT NULL,
                punched_at TEXT NOT NULL,
                direction TEXT NOT NULL,
                source TEXT NOT NULL,
                device_key TEXT UNIQUE,
                FOREIGN KEY (employee_id) REFERENCES employees(id)
            );
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS users (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                password_hash TEXT NOT NULL,
                app_role TEXT NOT NULL,
                employee_id INTEGER,
                is_active INTEGER NOT NULL DEFAULT 1,
                FOREIGN KEY (employee_id) REFERENCES employees(id)
            );
            """;
        cmd.ExecuteNonQuery();
        EnsureRatesColumn(connection);
        DemoSeeder.Ensure(connection);
        BackfillRoleRates(connection);
    }

    private static void EnsureRatesColumn(SqliteConnection connection)
    {
        var hasRates = false;
        using (var info = connection.CreateCommand())
        {
            info.CommandText = "PRAGMA table_info(roles)";
            using var reader = info.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "rates_json", StringComparison.OrdinalIgnoreCase))
                {
                    hasRates = true;
                    break;
                }
            }
        }

        if (hasRates)
        {
            return;
        }

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE roles ADD COLUMN rates_json TEXT";
        alter.ExecuteNonQuery();
    }

    private static void BackfillRoleRates(SqliteConnection connection)
    {
        using var select = connection.CreateCommand();
        select.CommandText = "SELECT id, hourly_rate, rates_json FROM roles";
        using var reader = select.ExecuteReader();
        var updates = new List<(long Id, string Json)>();
        while (reader.Read())
        {
            if (!reader.IsDBNull(2) && !string.IsNullOrWhiteSpace(reader.GetString(2)))
            {
                continue;
            }

            var ordinary = SqliteSession.ParseDecimal(reader.GetString(1));
            updates.Add((reader.GetInt64(0), RoleRates.FromOrdinary(ordinary).ToJson()));
        }

        reader.Close();
        foreach (var (id, json) in updates)
        {
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE roles SET rates_json = $j WHERE id = $id";
            update.Parameters.AddWithValue("$j", json);
            update.Parameters.AddWithValue("$id", id);
            update.ExecuteNonQuery();
        }
    }
}
