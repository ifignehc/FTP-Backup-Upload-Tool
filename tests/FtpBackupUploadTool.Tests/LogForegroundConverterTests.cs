using System.Globalization;
using System.Windows.Media;
using FtpBackupUploadTool.App.Converters;

namespace FtpBackupUploadTool.Tests;

internal static class LogForegroundConverterTests
{
    public static void WarningAndErrorLogsUseDistinctForegroundColors()
    {
        var converter = new LogForegroundConverter();

        TestAssert.Equal(
            Color.FromRgb(245, 158, 11),
            ConvertToBrush(converter, "12:00:00  [Warning] Check: message").Color,
            "Warning logs should be orange");
        TestAssert.Equal(
            Color.FromRgb(220, 38, 38),
            ConvertToBrush(converter, "12:00:01  [Error] Upload: message").Color,
            "Error logs should be red");
        TestAssert.Equal(
            Color.FromRgb(229, 231, 235),
            ConvertToBrush(converter, "12:00:02  [Normal] Backup: message").Color,
            "Normal logs should keep the default light foreground");
    }

    private static SolidColorBrush ConvertToBrush(LogForegroundConverter converter, string log)
    {
        return (SolidColorBrush)converter.Convert(log, typeof(SolidColorBrush), string.Empty, CultureInfo.InvariantCulture);
    }
}
