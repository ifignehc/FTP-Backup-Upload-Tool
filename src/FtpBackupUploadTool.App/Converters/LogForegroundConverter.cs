using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace FtpBackupUploadTool.App.Converters;

public sealed class LogForegroundConverter : IValueConverter
{
    private static readonly SolidColorBrush NormalBrush = CreateBrush(0xE5, 0xE7, 0xEB);
    private static readonly SolidColorBrush WarningBrush = CreateBrush(0xF5, 0x9E, 0x0B);
    private static readonly SolidColorBrush ErrorBrush = CreateBrush(0xDC, 0x26, 0x26);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var log = value as string ?? string.Empty;
        if (log.Contains("[Error]", StringComparison.OrdinalIgnoreCase))
        {
            return ErrorBrush;
        }

        if (log.Contains("[Warning]", StringComparison.OrdinalIgnoreCase))
        {
            return WarningBrush;
        }

        return NormalBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }
}
