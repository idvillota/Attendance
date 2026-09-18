using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Data;

internal sealed class SqliteSession
{
    private readonly string _connectionString;

    public SqliteSession(string filePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        FilePath = filePath;
        _connectionString = new SqliteConnectionStringBuilder { DataSource = filePath }.ToString();
    }

    public string FilePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    public static string FormatDecimal(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    public static decimal ParseDecimal(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    public static string FormatTime(DateTime value) => value.ToString("o", CultureInfo.InvariantCulture);

    public static bool GetBool(IReadOnlyDictionary<string, string> map, string key, bool fallback)
        => map.TryGetValue(key, out var value) ? value is "1" or "true" : fallback;

    public static int GetInt(IReadOnlyDictionary<string, string> map, string key, int fallback)
        => map.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;

    public static string GetString(IReadOnlyDictionary<string, string> map, string key, string fallback)
        => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    public static DateTime? GetTime(IReadOnlyDictionary<string, string> map, string key)
    {
        if (!map.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }
}
