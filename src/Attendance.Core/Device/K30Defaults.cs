namespace Attendance.Core.Device;

public static class K30Defaults
{
    public const string Ip = "192.168.39.201";
    public const int Port = 4370;
    public static string Endpoint => $"{Ip}:{Port}";
}
