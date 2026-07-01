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

    public static void CountsVisibleDirectoriesAndFiles()
    {
        var viewModel = CreatePane(
            Directory("assets"),
            Directory("logs"),
            File("assets/logo.png", 10, "2026-06-29T09:00:00+08:00"),
            File("readme.txt", 20, "2026-06-29T10:00:00+08:00"));

        TestAssert.Equal(2, viewModel.DirectoryCount, "directory count should include visible directories");
        TestAssert.Equal(2, viewModel.FileCount, "file count should include visible regular files");
        TestAssert.Equal("文件夹 2 个，文件 2 个", viewModel.ItemCountSummary, "summary should show directory and file counts");

        viewModel.FilterText = "assets";

        TestAssert.Equal(1, viewModel.DirectoryCount, "directory count should update after filtering");
        TestAssert.Equal(1, viewModel.FileCount, "file count should update after filtering");
        TestAssert.Equal("文件夹 1 个，文件 1 个", viewModel.ItemCountSummary, "summary should update after filtering");
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
