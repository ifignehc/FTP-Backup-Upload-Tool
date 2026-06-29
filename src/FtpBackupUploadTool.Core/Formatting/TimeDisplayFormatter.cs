using System.Globalization;

namespace FtpBackupUploadTool.Core.Formatting;

public static class TimeDisplayFormatter
{
    private static readonly TimeSpan BeijingOffset = TimeSpan.FromHours(8);

    public static string FormatBeijingTime(DateTimeOffset? value)
    {
        return value is null
            ? string.Empty
            : DateTime.SpecifyKind(value.Value.DateTime, DateTimeKind.Utc)
                .Add(BeijingOffset)
                .ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }
}
