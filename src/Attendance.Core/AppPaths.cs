namespace Attendance.Core;

public static class AppPaths
{
    public static string DataDirectory
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "HostAttendance");
            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string DatabaseFile => Path.Combine(DataDirectory, "attendance.db");
}
