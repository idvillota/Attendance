using Attendance.Core.Domain;

namespace Attendance.Core.Device;

internal static class PunchCsvParser
{
    public static IReadOnlyList<DevicePunch> ParseFile(string path) => Parse(File.ReadLines(path));

    public static IReadOnlyList<DevicePunch> Parse(IEnumerable<string> lines)
    {
        var logs = new List<DevicePunch>();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("DeviceUserId", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = line.Split(',', ';');
            if (parts.Length < 2)
            {
                continue;
            }

            var userId = parts[0].Trim().Trim('"');
            if (!DateTime.TryParse(parts[1].Trim().Trim('"'), out var at))
            {
                continue;
            }

            var direction = PunchDirection.Unknown;
            if (parts.Length >= 3)
            {
                var token = parts[2].Trim().Trim('"');
                direction = token.Equals("in", StringComparison.OrdinalIgnoreCase)
                    || token.Equals("entrada", StringComparison.OrdinalIgnoreCase)
                    ? PunchDirection.In
                    : token.Equals("out", StringComparison.OrdinalIgnoreCase)
                      || token.Equals("salida", StringComparison.OrdinalIgnoreCase)
                        ? PunchDirection.Out
                        : PunchDirection.Unknown;
            }

            logs.Add(new DevicePunch
            {
                DeviceUserId = userId,
                PunchedAt = at,
                Direction = direction,
                DeviceKey = $"csv:{userId}:{at:o}"
            });
        }

        return logs;
    }
}
