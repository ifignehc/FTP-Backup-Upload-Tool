using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Tests;

internal static class FileEntryTests
{
    public static void FileEntryFormatsUtcLastModifiedAsBeijingTime()
    {
        var entry = new FileEntry(
            RelativePath.Parse("css/site.css"),
            false,
            12,
            new DateTimeOffset(2026, 6, 29, 15, 36, 29, TimeSpan.Zero));

        TestAssert.Equal(
            "2026-06-29 23:36:29",
            entry.LastModifiedDisplay,
            "file list should show LastModified using the same Beijing-time format as the backup log");
    }

    public static void FileEntryDoesNotAddEightHoursToAlreadyBeijingLastModified()
    {
        var entry = new FileEntry(
            RelativePath.Parse("css/site.css"),
            false,
            12,
            new DateTimeOffset(2026, 6, 29, 15, 36, 29, TimeSpan.FromHours(8)));

        TestAssert.Equal(
            "2026-06-29 15:36:29",
            entry.LastModifiedDisplay,
            "FTP list timestamps parsed as local Beijing time should not be shifted by another 8 hours");
    }

    public static void FileEntryFormatsMissingLastModifiedAsBlank()
    {
        var entry = new FileEntry(RelativePath.Parse("code"), true, 0, null);

        TestAssert.Equal(string.Empty, entry.LastModifiedDisplay, "missing LastModified should display as a blank cell");
    }
}
