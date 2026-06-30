using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class MainViewModelTests
{
    public static void StartsWithEmptyPathList()
    {
        var viewModel = CreateViewModel();

        TestAssert.Equal(string.Empty, viewModel.PathListText, "path list should be empty when the app starts");
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
}
