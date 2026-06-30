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
