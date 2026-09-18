using System.Globalization;
using Avalonia.Data.Converters;

namespace Attendance.Desktop;

public sealed class BoolToSiNoConverter : IValueConverter
{
    public static readonly BoolToSiNoConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "Sí" : "No";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
