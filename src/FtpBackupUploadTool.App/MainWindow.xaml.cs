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
    private readonly AppConfigStore configStore = new(AppConfigStore.GetDefaultConfigPath());
    private readonly DpapiPasswordProtector passwordProtector = new();
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
            var config = await configStore.LoadAsync(CancellationToken.None);
            var selected = config.Processes.FirstOrDefault();

            if (selected is null)
            {
                viewModel.AddLog("[Warning] 未找到已保存工序，请打开设置完成配置");
                return;
            }

            LoadProcessRuntime(selected);
        }
        catch (Exception ex)
        {
            viewModel.AddLog($"[Error] 加载配置失败：{ex.Message}");
        }
    }

    private async void OnSettingsRequested(object? sender, EventArgs e)
    {
        var settingsWindow = new SettingsWindow(configStore, passwordProtector, viewModel.CurrentProcess)
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true)
        {
            try
            {
                var savedProcess = settingsWindow.SavedProcess
                    ?? (await configStore.LoadAsync(CancellationToken.None)).Processes.FirstOrDefault();
                if (savedProcess is null)
                {
                    viewModel.AddLog("[Warning] 设置已关闭，但没有找到可加载的工序配置");
                    return;
                }

                LoadProcessRuntime(savedProcess);
            }
            catch (Exception ex)
            {
                viewModel.AddLog($"[Error] 加载新配置失败：{ex.Message}");
            }
        }
    }

    private void LoadProcessRuntime(FtpBackupUploadTool.Core.Models.ProcessConfig process)
    {
        var factory = new ProcessRuntimeFactory(passwordProtector);
        viewModel.LoadProcess(process, factory.Create(process));
        viewModel.AddLog($"已加载配置：{process.Name}");
    }
}
