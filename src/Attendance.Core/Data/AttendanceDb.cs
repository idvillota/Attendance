using Attendance.Core.Auth;
using Attendance.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Data;

public sealed class AttendanceDb : IAttendanceStore, IUserStore
{
    private readonly SqliteSession _session;

    public AttendanceDb(string filePath)
    {
        _session = new SqliteSession(filePath);
    }

    public string FilePath => _session.FilePath;

    public void Initialize()
    {
        using var connection = _session.Open();
        SchemaMigrator.Apply(connection);
    }

    public IReadOnlyList<Role> GetRoles(bool activeOnly = false)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = activeOnly
            ? "SELECT id, name, hourly_rate, is_active, rates_json FROM roles WHERE is_active = 1 ORDER BY name"
            : "SELECT id, name, hourly_rate, is_active, rates_json FROM roles ORDER BY name";
        return ReadRoles(cmd);
    }

    public Role AddRole(string name, RoleRates? rates = null)
    {
        rates ??= RoleRates.FromOrdinary(RoleRates.OrdinaryColombia2026);
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO roles (name, hourly_rate, is_active, rates_json)
            VALUES ($n, $r, 1, $j);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$n", name.Trim());
        cmd.Parameters.AddWithValue("$r", SqliteSession.FormatDecimal(rates.OrdinaryDay));
        cmd.Parameters.AddWithValue("$j", rates.ToJson());
        var id = (long)cmd.ExecuteScalar()!;
        return new Role { Id = id, Name = name.Trim(), Rates = rates, IsActive = true };
    }

    public void UpdateRole(Role role)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE roles SET name = $n, hourly_rate = $r, is_active = $a, rates_json = $j WHERE id = $id";
        cmd.Parameters.AddWithValue("$n", role.Name.Trim());
        cmd.Parameters.AddWithValue("$r", SqliteSession.FormatDecimal(role.Rates.OrdinaryDay));
        cmd.Parameters.AddWithValue("$a", role.IsActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$j", role.Rates.ToJson());
        cmd.Parameters.AddWithValue("$id", role.Id);
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<Employee> GetEmployees(bool activeOnly = false)
    {
        using var connection = _session.Open();
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
        using var connection = _session.Open();
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
        using var connection = _session.Open();
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

    public EmployeeDeletePreview? GetEmployeeDeletePreview(long employeeId)
    {
        using var connection = _session.Open();
        using var lookup = connection.CreateCommand();
        lookup.CommandText = """
            SELECT e.id, e.name, e.device_user_id,
                   (SELECT COUNT(*) FROM punches p WHERE p.employee_id = e.id),
                   (SELECT GROUP_CONCAT(u.username, ', ') FROM users u WHERE u.employee_id = e.id)
            FROM employees e
            WHERE e.id = $id
            LIMIT 1
            """;
        lookup.Parameters.AddWithValue("$id", employeeId);
        using var reader = lookup.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var names = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        return new EmployeeDeletePreview
        {
            EmployeeId = reader.GetInt64(0),
            Name = reader.GetString(1),
            DeviceUserId = reader.GetString(2),
            PunchCount = Convert.ToInt32(reader.GetValue(3)),
            LinkedUsernames = SplitNames(names)
        };
    }

    public EmployeeDeleteResult DeleteEmployee(long employeeId)
    {
        var preview = GetEmployeeDeletePreview(employeeId)
            ?? throw new InvalidOperationException("La persona no existe.");

        using var connection = _session.Open();
        using var tx = connection.BeginTransaction();
        try
        {
            Execute(connection, tx, "DELETE FROM punches WHERE employee_id = $id", employeeId);
            Execute(connection, tx, """
                UPDATE users
                SET employee_id = NULL,
                    is_active = CASE WHEN app_role = 'Basic' THEN 0 ELSE is_active END
                WHERE employee_id = $id
                """, employeeId);
            Execute(connection, tx, "DELETE FROM employees WHERE id = $id", employeeId);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return new EmployeeDeleteResult
        {
            Name = preview.Name,
            DeviceUserId = preview.DeviceUserId,
            RemovedPunches = preview.PunchCount,
            UnlinkedUsernames = preview.LinkedUsernames
        };
    }

    public IReadOnlyList<Punch> GetPunches(DateTime? from = null, DateTime? toExclusive = null)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT p.id, p.employee_id, e.name, p.punched_at, p.direction, p.source, p.device_key
            FROM punches p
            JOIN employees e ON e.id = p.employee_id
            WHERE ($from IS NULL OR p.punched_at >= $from)
              AND ($to IS NULL OR p.punched_at < $to)
            ORDER BY p.punched_at DESC, p.id DESC
            """;
        cmd.Parameters.AddWithValue("$from", from is null ? DBNull.Value : SqliteSession.FormatTime(from.Value));
        cmd.Parameters.AddWithValue("$to", toExclusive is null ? DBNull.Value : SqliteSession.FormatTime(toExclusive.Value));
        using var reader = cmd.ExecuteReader();
        var list = new List<Punch>();
        while (reader.Read())
        {
            list.Add(new Punch
            {
                Id = reader.GetInt64(0),
                EmployeeId = reader.GetInt64(1),
                EmployeeName = reader.GetString(2),
                PunchedAt = DateTime.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind),
                Direction = Enum.Parse<PunchDirection>(reader.GetString(4)),
                Source = Enum.Parse<PunchSource>(reader.GetString(5)),
                DeviceKey = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        }

        return list;
    }

    public Punch AddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey = null)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO punches (employee_id, punched_at, direction, source, device_key)
            VALUES ($e, $t, $d, $s, $k);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$e", employeeId);
        cmd.Parameters.AddWithValue("$t", SqliteSession.FormatTime(punchedAt));
        cmd.Parameters.AddWithValue("$d", direction.ToString());
        cmd.Parameters.AddWithValue("$s", source.ToString());
        cmd.Parameters.AddWithValue("$k", (object?)deviceKey ?? DBNull.Value);
        var id = (long)cmd.ExecuteScalar()!;
        return GetPunches().Single(p => p.Id == id);
    }

    public int TryAddPunch(long employeeId, DateTime punchedAt, PunchDirection direction, PunchSource source, string? deviceKey)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT OR IGNORE INTO punches (employee_id, punched_at, direction, source, device_key)
            VALUES ($e, $t, $d, $s, $k);
            SELECT changes();
            """;
        cmd.Parameters.AddWithValue("$e", employeeId);
        cmd.Parameters.AddWithValue("$t", SqliteSession.FormatTime(punchedAt));
        cmd.Parameters.AddWithValue("$d", direction.ToString());
        cmd.Parameters.AddWithValue("$s", source.ToString());
        cmd.Parameters.AddWithValue("$k", (object?)deviceKey ?? DBNull.Value);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public void DeletePunch(long id)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM punches WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public AppSettings GetSettings()
    {
        var map = ReadSettingsMap();
        return new AppSettings
        {
            DeviceIp = SqliteSession.GetString(map, "device_ip", Device.K30Defaults.Ip),
            DevicePort = SqliteSession.GetInt(map, "device_port", Device.K30Defaults.Port),
            DeviceNumber = SqliteSession.GetInt(map, "device_number", 1),
            LastDownloadAt = SqliteSession.GetTime(map, "last_download_at"),
            Schedule = ScheduleSettingsMapper.FromMap(map)
        };
    }

    public void SaveSettings(AppSettings settings)
    {
        UpsertSetting("device_ip", settings.DeviceIp.Trim());
        UpsertSetting("device_port", settings.DevicePort.ToString(System.Globalization.CultureInfo.InvariantCulture));
        UpsertSetting("device_number", settings.DeviceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (settings.LastDownloadAt is { } lastDownload)
        {
            UpsertSetting("last_download_at", SqliteSession.FormatTime(lastDownload));
        }
    }

    public TenantSchedule GetSchedule() => GetSettings().Schedule;

    public void SaveSchedule(TenantSchedule schedule)
    {
        foreach (var (key, value) in ScheduleSettingsMapper.ToMap(schedule))
        {
            UpsertSetting(key, value);
        }
    }

    private Dictionary<string, string> ReadSettingsMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM settings";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            map[reader.GetString(0)] = reader.GetString(1);
        }

        return map;
    }

    private void UpsertSetting(string key, string value)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO settings (key, value) VALUES ($k, $v)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", value);
        cmd.ExecuteNonQuery();
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
                IsActive = reader.GetInt32(3) == 1,
                Rates = RoleRates.FromJson(reader.IsDBNull(4) ? null : reader.GetString(4), SqliteSession.ParseDecimal(reader.GetString(2)))
            });
        }

        return list;
    }

    public int CountUsers()
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public int CountActiveAdmins()
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM users WHERE app_role = 'Admin' AND is_active = 1";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public IReadOnlyList<AppUser> GetUsers()
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.username, u.password_hash, u.app_role, u.employee_id, e.name, u.is_active
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            ORDER BY u.username
            """;
        return ReadStoredUsers(cmd).Select(user => user.ToAppUser()).ToList();
    }

    public StoredUser? FindByUsername(string username)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.username, u.password_hash, u.app_role, u.employee_id, e.name, u.is_active
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            WHERE u.username = $u
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$u", username.Trim());
        return ReadStoredUsers(cmd).FirstOrDefault();
    }

    public StoredUser? GetStoredUser(long id)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT u.id, u.username, u.password_hash, u.app_role, u.employee_id, e.name, u.is_active
            FROM users u
            LEFT JOIN employees e ON e.id = u.employee_id
            WHERE u.id = $id
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$id", id);
        return ReadStoredUsers(cmd).FirstOrDefault();
    }

    public AppUser AddUser(string username, string passwordHash, AppRole role, long? employeeId)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO users (username, password_hash, app_role, employee_id, is_active)
            VALUES ($u, $p, $r, $e, 1);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$u", username.Trim());
        cmd.Parameters.AddWithValue("$p", passwordHash);
        cmd.Parameters.AddWithValue("$r", role.ToString());
        cmd.Parameters.AddWithValue("$e", (object?)employeeId ?? DBNull.Value);
        var id = (long)cmd.ExecuteScalar()!;
        return GetStoredUser(id)!.ToAppUser();
    }

    public void UpdateUser(long id, string username, string? passwordHash, AppRole role, long? employeeId, bool isActive)
    {
        using var connection = _session.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = passwordHash is null
            ? """
                UPDATE users
                SET username = $u, app_role = $r, employee_id = $e, is_active = $a
                WHERE id = $id
                """
            : """
                UPDATE users
                SET username = $u, password_hash = $p, app_role = $r, employee_id = $e, is_active = $a
                WHERE id = $id
                """;
        cmd.Parameters.AddWithValue("$u", username.Trim());
        cmd.Parameters.AddWithValue("$r", role.ToString());
        cmd.Parameters.AddWithValue("$e", (object?)employeeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$a", isActive ? 1 : 0);
        cmd.Parameters.AddWithValue("$id", id);
        if (passwordHash is not null)
        {
            cmd.Parameters.AddWithValue("$p", passwordHash);
        }

        cmd.ExecuteNonQuery();
    }

    private static List<StoredUser> ReadStoredUsers(SqliteCommand cmd)
    {
        using var reader = cmd.ExecuteReader();
        var list = new List<StoredUser>();
        while (reader.Read())
        {
            list.Add(new StoredUser
            {
                Id = reader.GetInt64(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                Role = Enum.Parse<AppRole>(reader.GetString(3)),
                EmployeeId = reader.IsDBNull(4) ? null : reader.GetInt64(4),
                EmployeeName = reader.IsDBNull(5) ? null : reader.GetString(5),
                IsActive = reader.GetInt32(6) == 1
            });
        }

        return list;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction tx, string sql, long employeeId)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("$id", employeeId);
        cmd.ExecuteNonQuery();
    }

    private static IReadOnlyList<string> SplitNames(string raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
