using System.IO;
using System.Windows;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.App.Views;
using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Security;
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
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var store = new AppConfigStore(AppConfigStore.GetDefaultConfigPath());
            var config = await store.LoadAsync(CancellationToken.None);
            var selected = config.Processes.FirstOrDefault();

            if (selected is null)
            {
                viewModel.AddLog("[Warning] 未找到已保存工序，请打开设置完成配置");
                return;
            }

            var factory = new ProcessRuntimeFactory(new DpapiPasswordProtector());
            viewModel.LoadProcess(selected, factory.Create(selected));
            viewModel.AddLog($"已加载配置：{selected.Name}");
        }
        catch (Exception ex)
        {
            viewModel.AddLog($"[Error] 加载配置失败：{ex.Message}");
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            viewModel.AddLog("设置窗口已关闭");
        }
    }
}
