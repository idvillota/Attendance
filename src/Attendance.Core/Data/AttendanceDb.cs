using System.Globalization;
using Attendance.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Data;

public sealed class AttendanceDb
{
    private readonly string _connectionString;

    public AttendanceDb(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = filePath }.ToString();
        FilePath = filePath;
    }

    public string FilePath { get; }

    public void Initialize()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS roles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                hourly_rate TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
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
            """;
        cmd.ExecuteNonQuery();
        EnsureDemoRoster(connection);
    }

    public IReadOnlyList<Role> GetRoles(bool activeOnly = false)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = activeOnly
            ? "SELECT id, name, hourly_rate, is_active FROM roles WHERE is_active = 1 ORDER BY name"
            : "SELECT id, name, hourly_rate, is_active FROM roles ORDER BY name";
        return ReadRoles(cmd);
    }

    public Role AddRole(string name, decimal hourlyRate)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO roles (name, hourly_rate, is_active) VALUES ($n, $r, 1); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.Parameters.AddWithValue("$r", FormatDecimal(hourlyRate));
        var id = (long)cmd.ExecuteScalar()!;
        return new Role { Id = id, Name = name.Trim(), HourlyRate = hourlyRate, IsActive = true };
    }

    public void UpdateRole(Role role)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE roles SET name = $n, hourly_rate = $r, is_active = $a WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", role.Name.Trim());
        cmd.Parameters.AddWithValue("$r", FormatDecimal(role.HourlyRate));
        cmd.Parameters.AddWithValue("$a", role.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", role.Id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Employee> GetEmployees(bool activeOnly = false)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT e.id, e.name, e.role_id, r.name, e.device_user_id, e.is_active
            FROM employees e
            JOIN roles r ON r.id = e.role_id
            """ + (activeOnly ? " WHERE e.is_active = 1" : "") + " ORDER BY e.name";
        using var reader = cmd.ExecuteReader();
        var list = new List<Employee>();
        while (reader.Read())
        {
            list.Add(new Employee
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                RoleId = reader.GetInt64(2),
                RoleName = reader.GetString(3),
                DeviceUserId = reader.GetString(4),
                IsActive = reader.GetInt32(5) == 1
            });
        }

        return list;
    }

    public Employee AddEmployee(string name, long roleId, string deviceUserId)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO employees (name, role_id, device_user_id, is_active)
            VALUES ($n, $r, $d, 1);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.Parameters.AddWithValue("$r", roleId);
        cmd.Parameters.AddWithValue("$d", deviceUserId.Trim());
        var id = (long)cmd.ExecuteScalar()!;
        return GetEmployees().Single(e => e.Id == id);
    }

    public void UpdateEmployee(Employee employee)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE employees
            SET name = $n, role_id = $r, device_user_id = $d, is_active = $a
            WHERE id = $id
            """;
        cmd.Parameters.AddWithValue("$n", employee.Name.Trim());
        cmd.Parameters.AddWithValue("$r", employee.RoleId);
        cmd.Parameters.AddWithValue("$d", employee.DeviceUserId.Trim());
        cmd.Parameters.AddWithValue("$a", employee.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", employee.Id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Punch> GetPunches(DateTime? from = null, DateTime? toExclusive = null)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT p.id, p.employee_id, e.name, p.punched_at, p.direction, p.source, p.device_key
            FROM punches p
            JOIN employees e ON e.id = p.employee_id
            WHERE ($from IS NULL OR p.punched_at >= $from)
              AND ($to IS NULL OR p.punched_at < $to)
            ORDER BY p.punched_at DESC, p.id DESC
            """;
        cmd.Parameters.AddWithValue("$from", from is null ? DBNull.Value : FormatTime(from.Value));
        cmd.Parameters.AddWithValue("$to", toExclusive is null ? DBNull.Value : FormatTime(toExclusive.Value));
        using var reader = cmd.ExecuteReader();
        var list = new List<Punch>();
        while (reader.Read())
        {
            list.Add(new Punch
            {
                Id = reader.GetInt64(0),
                EmployeeId = reader.GetInt64(1),
                EmployeeName = reader.GetString(2),
                PunchedAt = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Direction = Enum.Parse<PunchDirection>(reader.GetString(4)),
                Source = Enum.Parse<PunchSource>(reader.GetString(5)),
                DeviceKey = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return list;
    }

    public Punch AddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey = null)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO punches (employee_id, punched_at, direction, source, device_key)
            VALUES ($e, $t, $d, $s, $k);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$e", employeeId);
        cmd.Parameters.AddWithValue("$t", FormatTime(punchedAt));
        cmd.Parameters.AddWithValue("$d", direction.ToString());
        cmd.Parameters.AddWithValue("$s", source.ToString());
        cmd.Parameters.AddWithValue("$k", (object?)deviceKey ?? DBNull.Value);
        var id = (long)cmd.ExecuteScalar()!;
        return GetPunches().Single(p => p.Id == id);
    }

    public int TryAddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO punches (employee_id, punched_at, direction, source, device_key)
            VALUES ($e, $t, $d, $s, $k);
            SELECT changes();
            """;
        cmd.Parameters.AddWithValue("$e", employeeId);
        cmd.Parameters.AddWithValue("$t", FormatTime(punchedAt));
        cmd.Parameters.AddWithValue("$d", direction.ToString());
        cmd.Parameters.AddWithValue("$s", source.ToString());
        cmd.Parameters.AddWithValue("$k", (object?)deviceKey ?? DBNull.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void DeletePunch(long id)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM punches WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public AppSettings GetSettings()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            map[reader.GetString(0)] = reader.GetString(1);
        }

        return new AppSettings
        {
            UseMockDevice = GetBool(map, "use_mock_device", true),
            DeviceIp = GetString(map, "device_ip", "192.168.1.201"),
            DevicePort = GetInt(map, "device_port", 4370),
            DeviceNumber = GetInt(map, "device_number", 1)
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        UpsertSetting("use_mock_device", settings.UseMockDevice ? "1" : "0");
        UpsertSetting("device_ip", settings.DeviceIp.Trim());
        UpsertSetting("device_port", settings.DevicePort.ToString(CultureInfo.InvariantCulture));
        UpsertSetting("device_number", settings.DeviceNumber.ToString(CultureInfo.InvariantCulture));
    }

    private void UpsertSetting(string key, string value)
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private static void EnsureDemoRoster(SqliteConnection connection)
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
        insert.CommandText = "INSERT INTO roles (name, hourly_rate, is_active) VALUES ($n, $r, 1); SELECT last_insert_rowid();";
        insert.Parameters.AddWithValue("$n", name);
        insert.Parameters.AddWithValue("$r", hourlyRate);
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

        var monday = WeekStart(DateOnly.FromDateTime(DateTime.Now));
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
        pT.Value = FormatTime(at);
        pD.Value = direction;
        pK.Value = $"seed:{employeeId}:{FormatTime(at)}";
        punch.ExecuteNonQuery();
    }

    private static DateOnly WeekStart(DateOnly date)
    {
        var offset = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-offset);
    }

    private static List<Role> ReadRoles(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<Role>();
        while (reader.Read())
        {
            list.Add(new Role
            {
                Id = reader.GetInt64(0),
                Name = reader.GetString(1),
                HourlyRate = ParseDecimal(reader.GetString(2)),
                IsActive = reader.GetInt32(3) == 1
            });
        }

        return list;
    }

    private static string FormatDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    private static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static string FormatTime(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    private static bool GetBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
        => map.TryGetValue(key, out var value) ? value is "1" or "true" : fallback;

    private static int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string GetString(IReadOnlyDictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}
