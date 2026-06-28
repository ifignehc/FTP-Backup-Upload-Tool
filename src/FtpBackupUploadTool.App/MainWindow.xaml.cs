using System.IO;
using System.Windows;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.App.Views;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        var devRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FtpBackupUploadTool",
            "DevMirror");
        var production = new LocalMirrorRemoteClient(Path.Combine(devRoot, "production"));
        var draft = new LocalMirrorRemoteClient(Path.Combine(devRoot, "draft"));
        var local = Path.Combine(devRoot, "local");
        Directory.CreateDirectory(local);

        viewModel = new MainViewModel(
            new BackupService(production, new BackupLogWriter()),
            new UploadService(draft, local),
            new CheckService(production, draft));
        viewModel.SettingsRequested += OnSettingsRequested;
        DataContext = viewModel;
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            viewModel.AddLog("设置已保存");
        }
    }
}
