using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.Tests;

internal static class MainViewModelTests
{
    public static void StartsWithEmptyPathList()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ftp-tool-main-vm", Guid.NewGuid().ToString("N"));
        var productionRoot = Path.Combine(tempRoot, "production");
        var draftRoot = Path.Combine(tempRoot, "draft");
        var localRoot = Path.Combine(tempRoot, "local");
        Directory.CreateDirectory(productionRoot);
        Directory.CreateDirectory(draftRoot);
        Directory.CreateDirectory(localRoot);

        var viewModel = new MainViewModel(
            new BackupService(new LocalMirrorRemoteClient(productionRoot), new BackupLogWriter()),
            new UploadService(new LocalMirrorRemoteClient(draftRoot), localRoot),
            new CheckService(new LocalMirrorRemoteClient(productionRoot), new LocalMirrorRemoteClient(draftRoot)));

        TestAssert.Equal(string.Empty, viewModel.PathListText, "path list should be empty when the app starts");
    }
}
