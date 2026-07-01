using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class MainViewModelTests
{
    public static void StartsWithEmptyPathList()
    {
        var viewModel = CreateViewModel();

        TestAssert.Equal(string.Empty, viewModel.PathListText, "path list should be empty when the app starts");
        TestAssert.Equal("0 个路径", viewModel.PathListCountDisplay, "path count should start at zero");
    }

    public static void PathListCountUpdatesWhenTextChanges()
    {
        var viewModel = CreateViewModel();
        var countChanges = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.PathListCountDisplay))
            {
                countChanges++;
            }
        };

        viewModel.PathListText = "css/site.css\r\n\r\njs/app.js, images/logo.png\r\ncss/site.css";

        TestAssert.Equal("3 个路径", viewModel.PathListCountDisplay, "path count should reflect parsed unique paths");
        TestAssert.Equal(1, countChanges, "path count should notify when the path list text changes");
    }

    public static void ConsolidatePathListRemovesDuplicatesAndBlankLines()
    {
        var viewModel = CreateViewModel();
        viewModel.PathListText = " css/site.css \r\nCSS/site.css\r\n\r\njs\\app.js, images/logo.png\r\n/images/logo.png";

        viewModel.ConsolidatePathListCommand.Execute(null);

        TestAssert.Equal(
            "css/site.css\r\njs/app.js\r\nimages/logo.png",
            viewModel.PathListText,
            "consolidate should normalize separators, remove blanks, and keep the first duplicate path");
        TestAssert.Equal("3 个路径", viewModel.PathListCountDisplay, "path count should update after consolidation");
    }

    public static void WorkflowCommandsRequireNonBlankPathList()
    {
        var viewModel = CreateViewModel();

        TestAssert.True(!viewModel.BackupCommand.CanExecute(null), "backup should be disabled for an empty path list");
        TestAssert.True(!viewModel.UploadCommand.CanExecute(null), "upload should be disabled for an empty path list");
        TestAssert.True(!viewModel.CheckCommand.CanExecute(null), "check should be disabled for an empty path list");

        viewModel.PathListText = "css/site.css";

        TestAssert.True(viewModel.BackupCommand.CanExecute(null), "backup should be enabled when the path list has a path");
        TestAssert.True(viewModel.UploadCommand.CanExecute(null), "upload should be enabled when the path list has a path");
        TestAssert.True(viewModel.CheckCommand.CanExecute(null), "check should be enabled when the path list has a path");

        viewModel.PathListText = " \r\n\t ";

        TestAssert.True(!viewModel.BackupCommand.CanExecute(null), "backup should be disabled for a whitespace-only path list");
        TestAssert.True(!viewModel.UploadCommand.CanExecute(null), "upload should be disabled for a whitespace-only path list");
        TestAssert.True(!viewModel.CheckCommand.CanExecute(null), "check should be disabled for a whitespace-only path list");
    }

    public static void PathListTextChangeRaisesWorkflowCommandCanExecuteChanged()
    {
        var viewModel = CreateViewModel();
        var backupChanges = 0;
        var uploadChanges = 0;
        var checkChanges = 0;
        viewModel.BackupCommand.CanExecuteChanged += (_, _) => backupChanges++;
        viewModel.UploadCommand.CanExecuteChanged += (_, _) => uploadChanges++;
        viewModel.CheckCommand.CanExecuteChanged += (_, _) => checkChanges++;

        viewModel.PathListText = "css/site.css";

        TestAssert.Equal(1, backupChanges, "backup command should notify can-execute changes when paths change");
        TestAssert.Equal(1, uploadChanges, "upload command should notify can-execute changes when paths change");
        TestAssert.Equal(1, checkChanges, "check command should notify can-execute changes when paths change");
    }

    public static void LoadProcessInitializesBackupPaneFromBackupDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var localRoot = Path.Combine(tempRoot, "local");
        var backupRoot = Path.Combine(tempRoot, "backups");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(draftRoot);
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(backupRoot);

        var viewModel = CreateLoadedViewModel(
            new LocalMirrorRemoteClient(productionRoot),
            new LocalMirrorRemoteClient(draftRoot),
            localRoot,
            backupDirectory: backupRoot);

        TestAssert.Equal(
            Path.GetFullPath(backupRoot),
            viewModel.BackupPane.CurrentPath,
            "the fourth commander pane should open the configured backup directory");
    }

    public static void RefreshFilePanesListsBackupPaneDirectory()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var localRoot = Path.Combine(tempRoot, "local");
        var backupRoot = Path.Combine(tempRoot, "backups");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(draftRoot);
        Directory.CreateDirectory(localRoot);
        Directory.CreateDirectory(Path.Combine(backupRoot, "20260630_Backup"));
        File.WriteAllText(Path.Combine(backupRoot, "notes.md"), "backup notes");

        var viewModel = CreateLoadedViewModel(
            new LocalMirrorRemoteClient(productionRoot),
            new LocalMirrorRemoteClient(draftRoot),
            localRoot,
            backupDirectory: backupRoot);

        viewModel.RefreshFilePanesAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(
            viewModel.BackupPane.Files.Any(file => file.IsDirectory && file.DisplayName == "20260630_Backup/"),
            "backup pane should list backup folders");
        TestAssert.True(
            viewModel.BackupPane.Files.Any(file => !file.IsDirectory && file.DisplayName == "notes.md"),
            "backup pane should list backup files");
    }

    public static void UploadUsesCurrentLocalPaneDirectoryAsLocalRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var configuredLocalRoot = Path.Combine(tempRoot, "configured-local");
        var currentLocalRoot = Path.Combine(tempRoot, "current-local");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        Directory.CreateDirectory(configuredLocalRoot);
        Directory.CreateDirectory(Path.Combine(currentLocalRoot, "css"));
        File.WriteAllText(Path.Combine(currentLocalRoot, "css", "site.css"), "current-local-body");

        var draft = new LocalMirrorRemoteClient(draftRoot);
        var production = new LocalMirrorRemoteClient(productionRoot);
        var viewModel = CreateLoadedViewModel(production, draft, configuredLocalRoot);
        viewModel.LocalPane.CurrentPath = currentLocalRoot;
        viewModel.PathListText = "css/site.css";

        RunPrivateWorkflow(viewModel, "RunUploadCoreAsync");

        TestAssert.Equal(
            "current-local-body",
            File.ReadAllText(Path.Combine(draftRoot, "css", "site.css")),
            "upload should read files from the currently displayed local directory");
    }

    public static void CheckUsesCurrentLocalPaneDirectoryAsLocalRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var configuredLocalRoot = Path.Combine(tempRoot, "configured-local");
        var currentLocalRoot = Path.Combine(tempRoot, "current-local");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(draftRoot);
        Directory.CreateDirectory(configuredLocalRoot);
        Directory.CreateDirectory(Path.Combine(currentLocalRoot, "images"));
        File.WriteAllText(Path.Combine(currentLocalRoot, "images", "new.png"), "current-local-image");

        var draft = new LocalMirrorRemoteClient(draftRoot);
        var production = new LocalMirrorRemoteClient(productionRoot);
        var viewModel = CreateLoadedViewModel(production, draft, configuredLocalRoot);
        viewModel.LocalPane.CurrentPath = currentLocalRoot;
        viewModel.PathListText = "docs/missing.txt";

        RunPrivateWorkflow(viewModel, "RunCheckCoreAsync");

        TestAssert.True(
            viewModel.Logs.Any(log => log.Contains("images/new.png", StringComparison.OrdinalIgnoreCase)),
            "check should report local files missing from the path list under the currently displayed local directory");
    }

    public static void CheckWritesConfiguredMarkdownLog()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var localRoot = Path.Combine(tempRoot, "local");
        var logRoot = Path.Combine(tempRoot, "check-logs");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        Directory.CreateDirectory(localRoot);
        File.WriteAllText(Path.Combine(draftRoot, "css", "site.css"), "draft-body");

        var draft = new LocalMirrorRemoteClient(draftRoot);
        var production = new LocalMirrorRemoteClient(productionRoot);
        var viewModel = CreateLoadedViewModel(
            production,
            draft,
            localRoot,
            new CheckLogConfig(logRoot, "CheckReport"));
        viewModel.LocalPane.CurrentPath = localRoot;
        viewModel.PathListText = "css/site.css";

        RunPrivateWorkflow(viewModel, "RunCheckCoreAsync");

        var logs = Directory.GetFiles(logRoot, "CheckReport*.md", SearchOption.TopDirectoryOnly);
        TestAssert.Equal(1, logs.Length, "check workflow should write one configured Markdown log");
        var markdown = File.ReadAllText(logs[0]);
        TestAssert.True(markdown.Contains("## Updated Files", StringComparison.Ordinal), "check log should include updated files section");
        TestAssert.True(markdown.Contains("css/site.css", StringComparison.Ordinal), "check log should include checked path");
        TestAssert.True(
            viewModel.Logs.Any(log => log.Contains("检查日志已保存", StringComparison.Ordinal) && log.Contains(logs[0], StringComparison.Ordinal)),
            "main log should tell the user where the check log was saved");
    }

    public static void UploadStillUsesLocalPaneWhenBackupPanePointsElsewhere()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var configuredLocalRoot = Path.Combine(tempRoot, "configured-local");
        var currentLocalRoot = Path.Combine(tempRoot, "current-local");
        var backupRoot = Path.Combine(tempRoot, "backups");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(Path.Combine(draftRoot, "css"));
        Directory.CreateDirectory(configuredLocalRoot);
        Directory.CreateDirectory(Path.Combine(currentLocalRoot, "css"));
        Directory.CreateDirectory(Path.Combine(backupRoot, "css"));
        File.WriteAllText(Path.Combine(currentLocalRoot, "css", "site.css"), "current-local-body");
        File.WriteAllText(Path.Combine(backupRoot, "css", "site.css"), "backup-body");

        var viewModel = CreateLoadedViewModel(
            new LocalMirrorRemoteClient(productionRoot),
            new LocalMirrorRemoteClient(draftRoot),
            configuredLocalRoot,
            backupDirectory: backupRoot);
        viewModel.LocalPane.CurrentPath = currentLocalRoot;
        viewModel.BackupPane.CurrentPath = backupRoot;
        viewModel.PathListText = "css/site.css";

        RunPrivateWorkflow(viewModel, "RunUploadCoreAsync");

        TestAssert.Equal(
            "current-local-body",
            File.ReadAllText(Path.Combine(draftRoot, "css", "site.css")),
            "upload must keep using the local work pane instead of the new backup pane");
    }

    private static MainViewModel CreateViewModel()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var localRoot = Path.Combine(tempRoot, "local");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(draftRoot);
        Directory.CreateDirectory(localRoot);

        return new MainViewModel(
            new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter()),
            new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot),
            new CheckService(new LocalMirrorRemoteClient(productionRoot), new LocalMirrorRemoteClient(draftRoot)));
    }

    private static MainViewModel CreateLoadedViewModel(
        LocalMirrorRemoteClient production,
        LocalMirrorRemoteClient draft,
        string configuredLocalRoot,
        CheckLogConfig? checkLog = null,
        string? backupDirectory = null)
    {
        var viewModel = new MainViewModel(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, configuredLocalRoot),
            new CheckService(production, draft, configuredLocalRoot));
        var config = new ProcessConfig(
            "test",
            new ServerConfig("prod", 21, "prod-user", string.Empty, "/www"),
            new ServerConfig("draft", 21, "draft-user", string.Empty, "/www"),
            configuredLocalRoot,
            string.Empty,
            new BackupConfig(backupDirectory ?? Path.GetTempPath(), "{yyyy}{MM}{dd}", LogFieldOptions.All),
            checkLog ?? new CheckLogConfig(Path.GetTempPath(), "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Check"));
        var services = new WorkflowServices(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, configuredLocalRoot),
            new CheckService(production, draft, configuredLocalRoot),
            production,
            draft);

        viewModel.LoadProcess(config, services);
        return viewModel;
    }

    private static void RunPrivateWorkflow(MainViewModel viewModel, string methodName)
    {
        var method = typeof(MainViewModel).GetMethod(
            methodName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        TestAssert.True(method is not null, $"{methodName} should exist");
        ((Task)method!.Invoke(viewModel, Array.Empty<object>())!).GetAwaiter().GetResult();
    }
}
