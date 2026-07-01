using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.Tests;

internal static class FilePaneViewModelTests
{
    public static void SortByNameTogglesDirection()
    {
        var viewModel = CreatePane(
            Directory("assets"),
            File("zeta.txt", 10, "2026-06-29T09:00:00+08:00"),
            File("alpha.txt", 20, "2026-06-29T10:00:00+08:00"));

        viewModel.SortBy(FilePaneSortColumn.Name);

        TestAssert.Equal(
            "zeta.txt",
            viewModel.FilteredFiles[1].DisplayName,
            "clicking the name header again should sort files by name descending");
        TestAssert.Equal(
            "assets/",
            viewModel.FilteredFiles[0].DisplayName,
            "directories should stay before files when sorting by name descending");
    }

    public static void SortBySizeOrdersFilesByNumericSize()
    {
        var viewModel = CreatePane(
            Directory("assets"),
            File("small.txt", 10, "2026-06-29T09:00:00+08:00"),
            File("large.txt", 200, "2026-06-29T10:00:00+08:00"));

        viewModel.SortBy(FilePaneSortColumn.Size);

        TestAssert.Equal(
            "small.txt",
            viewModel.FilteredFiles[1].DisplayName,
            "size sorting should use numeric size instead of the formatted size text");
        TestAssert.Equal("large.txt", viewModel.FilteredFiles[2].DisplayName, "larger files should follow smaller files");
    }

    public static void SortByLastModifiedOrdersFilesByTimestamp()
    {
        var viewModel = CreatePane(
            File("newer.txt", 10, "2026-06-29T10:00:00+08:00"),
            File("older.txt", 10, "2026-06-29T09:00:00+08:00"));

        viewModel.SortBy(FilePaneSortColumn.LastModified);

        TestAssert.Equal(
            "older.txt",
            viewModel.FilteredFiles[0].DisplayName,
            "modified-time sorting should use the raw timestamp instead of the display string");
        TestAssert.Equal("newer.txt", viewModel.FilteredFiles[1].DisplayName, "newer files should follow older files");
    }

    private static FilePaneViewModel CreatePane(params FileEntry[] files)
    {
        return new FilePaneViewModel("test", false, files);
    }

    private static FileEntry Directory(string path)
    {
        return new FileEntry(RelativePath.Parse(path), true, 0, null);
    }

    private static FileEntry File(string path, long size, string lastModified)
    {
        return new FileEntry(RelativePath.Parse(path), false, size, DateTimeOffset.Parse(lastModified));
    }
}
