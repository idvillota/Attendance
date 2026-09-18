using System.Globalization;
using Attendance.Core.Domain;
using Attendance.Core.Payroll;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Data;

internal static class DemoSeeder
{
    public static void Ensure(SqliteConnection connection)
    {
        var mesero = EnsureRole(connection, "Mesero", "8000");
        var cocinero = EnsureRole(connection, "Cocinero", "10000");
        var cajero = EnsureRole(connection, "Cajero", "9000");
        var auxiliar = EnsureRole(connection, "Auxiliar", "7500");

        var ana = EnsureEmployee(connection, "Ana Pérez", mesero, "1");
        var carlos = EnsureEmployee(connection, "Carlos Gómez", cocinero, "2");
        var lucia = EnsureEmployee(connection, "Lucía Rojas", cajero, "3");
        var diego = EnsureEmployee(connection, "Diego Martínez", mesero, "4");
        var sofia = EnsureEmployee(connection, "Sofía Herrera", auxiliar, "5");

        SeedWeekIfEmpty(connection, ana, leaveFridayOpen: false);
        SeedWeekIfEmpty(connection, carlos, leaveFridayOpen: false);
        SeedWeekIfEmpty(connection, lucia, leaveFridayOpen: false);
        SeedWeekIfEmpty(connection, diego, leaveFridayOpen: false);
        SeedWeekIfEmpty(connection, sofia, leaveFridayOpen: true);
    }

    private static long EnsureRole(SqliteConnection connection, string name, string hourlyRate)
    {
        using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT id FROM roles WHERE name = $n LIMIT 1";
        lookup.Parameters.AddWithValue("$n", name);
        var existing = lookup.ExecuteScalar();
        if (existing is not null)
        {
            return Convert.ToInt64(existing);
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO roles (name, hourly_rate, is_active, rates_json)
            VALUES ($n, $r, 1, $j);
            SELECT last_insert_rowid();
            """;
        var rates = RoleRates.FromOrdinary(decimal.Parse(hourlyRate, CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$n", name);
        insert.Parameters.AddWithValue("$r", hourlyRate);
        insert.Parameters.AddWithValue("$j", rates.ToJson());
        return (long)insert.ExecuteScalar()!;
    }

    private static long EnsureEmployee(SqliteConnection connection, string name, long roleId, string deviceUserId)
    {
        using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT id FROM employees WHERE device_user_id = $d LIMIT 1";
        lookup.Parameters.AddWithValue("$d", deviceUserId);
        var existing = lookup.ExecuteScalar();
        if (existing is not null)
        {
            return Convert.ToInt64(existing);
        }

        using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO employees (name, role_id, device_user_id, is_active)
            VALUES ($n, $r, $d, 1);
            SELECT last_insert_rowid();
            """;
        insert.Parameters.AddWithValue("$n", name);
        insert.Parameters.AddWithValue("$r", roleId);
        insert.Parameters.AddWithValue("$d", deviceUserId);
        return (long)insert.ExecuteScalar()!;
    }

    private static void SeedWeekIfEmpty(SqliteConnection connection, long employeeId, bool leaveFridayOpen)
    {
        using var count = connection.CreateCommand();
        count.CommandText = "SELECT COUNT(*) FROM punches WHERE employee_id = $e";
        count.Parameters.AddWithValue("$e", employeeId);
        if (Convert.ToInt32(count.ExecuteScalar()) > 0)
        {
            return;
        }

        using var punch = connection.CreateCommand();
        punch.CommandText = """
            INSERT INTO punches (employee_id, punched_at, direction, source, device_key)
            VALUES ($e, $t, $d, 'Mock', $k)
            """;
        var pE = punch.Parameters.Add("$e", SqliteType.Integer);
        var pT = punch.Parameters.Add("$t", SqliteType.Text);
        var pD = punch.Parameters.Add("$d", SqliteType.Text);
        var pK = punch.Parameters.Add("$k", SqliteType.Text);

        var monday = WeekRange.StartOfWeek(DateOnly.FromDateTime(DateTime.Now));
        for (var day = 0; day < 5; day++)
        {
            var date = monday.AddDays(day);
            var clockIn = date.ToDateTime(new TimeOnly(8, 0)).AddMinutes(employeeId * 3);
            InsertPunch(punch, pE, pT, pD, pK, employeeId, clockIn, "In");
            if (leaveFridayOpen && day == 4)
            {
                continue;
            }

            var clockOut = date.ToDateTime(new TimeOnly(16, 0)).AddMinutes(employeeId * -2);
            InsertPunch(punch, pE, pT, pD, pK, employeeId, clockOut, "Out");
        }

        var sunday = monday.AddDays(6);
        InsertPunch(punch, pE, pT, pD, pK, employeeId, sunday.ToDateTime(new TimeOnly(20, 0)), "In");
        InsertPunch(punch, pE, pT, pD, pK, employeeId, sunday.ToDateTime(new TimeOnly(21, 0)), "Out");
    }

    private static void InsertPunch(
        SqliteCommand punch,
        SqliteParameter pE,
        SqliteParameter pT,
        SqliteParameter pD,
        SqliteParameter pK,
        long employeeId,
        DateTime at,
        string direction)
    {
        pE.Value = employeeId;
        pT.Value = SqliteSession.FormatTime(at);
        pD.Value = direction;
        pK.Value = $"seed:{employeeId}:{SqliteSession.FormatTime(at)}";
        punch.ExecuteNonQuery();
    }
}
