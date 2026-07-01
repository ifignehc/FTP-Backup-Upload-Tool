using System.Xml.Linq;
using System.Windows.Input;
using FtpBackupUploadTool.App;

namespace FtpBackupUploadTool.Tests;

internal static class MainWindowTests
{
    public static void MainWindowTitleIsFtpBuTool()
    {
        var xamlPath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml");
        var document = XDocument.Load(xamlPath);
        var title = document.Root?.Attribute("Title")?.Value;

        TestAssert.Equal("FTP BU Tool", title, "Main window title should match the product name");
    }

    public static void InitialRemoteRefreshTimeoutAllowsSlowCompanyFtpListing()
    {
        var codePath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml.cs");
        var code = File.ReadAllText(codePath);

        TestAssert.True(
            code.Contains("TimeSpan.FromSeconds(60)", StringComparison.Ordinal),
            "initial remote file pane refresh should allow slow company FTP directory listing");
    }

    public static void WindowShortcutsUseCtrlCopyPasteWithoutF5Copy()
    {
        TestAssert.Equal(
            WindowShortcutAction.None,
            MainWindowShortcutMapper.Resolve(Key.F5, ModifierKeys.None),
            "F5 should not trigger copy from the keyboard");

        TestAssert.Equal(
            WindowShortcutAction.Copy,
            MainWindowShortcutMapper.Resolve(Key.C, ModifierKeys.Control),
            "Ctrl+C should copy selected files from the active pane");

        TestAssert.Equal(
            WindowShortcutAction.Paste,
            MainWindowShortcutMapper.Resolve(Key.V, ModifierKeys.Control),
            "Ctrl+V should paste files into the active pane");
    }

    public static void ActiveFilePaneUsesBlueLeftFrameWithoutTextLabel()
    {
        var xamlPath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml");
        var codePath = FindRepositoryFile("src", "FtpBackupUploadTool.App", "MainWindow.xaml.cs");
        var xaml = File.ReadAllText(xamlPath);
        var code = File.ReadAllText(codePath);

        foreach (var frameName in new[]
        {
            "ProductionPaneFrame",
            "DraftPaneFrame",
            "LocalPaneFrame",
            "BackupPaneFrame"
        })
        {
            TestAssert.True(
                xaml.Contains($"x:Name=\"{frameName}\"", StringComparison.Ordinal),
                $"{frameName} should wrap a file pane so the active pane can show a left frame");
        }

        TestAssert.True(
            xaml.Contains("ActivePaneBorderBrush", StringComparison.Ordinal)
            && xaml.Contains("#2563EB", StringComparison.Ordinal),
            "active pane frame should use the selected blue accent");
        TestAssert.True(
            code.Contains("new(8, 2, 2, 2)", StringComparison.Ordinal),
            "active pane frame should use a thick left bar and light surrounding frame");
        TestAssert.True(
            code.Contains("UpdateActiveFilePaneFrame", StringComparison.Ordinal),
            "activating a file pane should update the visible frame");
        TestAssert.True(
            !xaml.Contains("当前操作", StringComparison.Ordinal),
            "active pane highlight should not render an explicit current-operation text label");
    }

    public static void LocalCopyTargetMessagesNameBackupPaneSeparately()
    {
        var paneKindType = typeof(MainWindow).Assembly.GetType("FtpBackupUploadTool.App.FilePaneKind");
        TestAssert.True(paneKindType is not null, "FilePaneKind should exist");
        var filePaneKindType = paneKindType!;
        var localPane = Enum.Parse(filePaneKindType, "Local");
        var backupPane = Enum.Parse(filePaneKindType, "Backup");
        var method = typeof(MainWindow).GetMethod(
            "GetLocalCopyTargetMessage",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);

        TestAssert.True(method is not null, "copy log target message helper should exist");
        TestAssert.Equal(
            "已复制到本地窗口",
            (string)method!.Invoke(null, new[] { localPane })!,
            "copying into the local pane should keep the local-window log message");
        TestAssert.Equal(
            "已复制到备份 / 对照窗口",
            (string)method.Invoke(null, new[] { backupPane })!,
            "copying into the backup pane should name the backup/compare window in the log");
    }

    private static string FindRepositoryFile(params string[] relativeParts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file.", Path.Combine(relativeParts));
    }
}
