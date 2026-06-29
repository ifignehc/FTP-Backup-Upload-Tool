using System.Windows;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly AppConfigStore configStore;
    private readonly IPasswordProtector passwordProtector;
    private readonly SettingsViewModel viewModel = new();
    private readonly ProcessConfig? currentProcess;

    public SettingsWindow()
        : this(
            new AppConfigStore(AppConfigStore.GetDefaultConfigPath()),
            new DpapiPasswordProtector(),
            currentProcess: null)
    {
    }

    public SettingsWindow(
        AppConfigStore configStore,
        IPasswordProtector passwordProtector,
        ProcessConfig? currentProcess)
    {
        this.configStore = configStore;
        this.passwordProtector = passwordProtector;
        this.currentProcess = currentProcess;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;

        if (currentProcess is not null)
        {
            viewModel.LoadProcess(currentProcess);
        }
    }

    public ProcessConfig? SavedProcess { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = await configStore.LoadAsync(CancellationToken.None);
            viewModel.LoadProcesses(config.Processes, currentProcess?.Name);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "加载设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var existingConfig = await configStore.LoadAsync(CancellationToken.None);
            var existingProcess = existingConfig.Processes.FirstOrDefault(process =>
                string.Equals(process.Name, viewModel.SelectedProcess, StringComparison.OrdinalIgnoreCase));
            var process = viewModel.BuildProcessConfig(
                passwordProtector,
                ProductionPasswordBox.Password,
                DraftPasswordBox.Password,
                existingProcess);
            var duplicateProcess = existingConfig.Processes.FirstOrDefault(item =>
                !string.Equals(item.Name, existingProcess?.Name, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.Name, process.Name, StringComparison.OrdinalIgnoreCase));
            if (duplicateProcess is not null)
            {
                throw new InvalidOperationException("工序名称已存在，请使用其他名称。");
            }

            var processes = existingConfig.Processes
                .Where(item => !string.Equals(item.Name, existingProcess?.Name ?? process.Name, StringComparison.OrdinalIgnoreCase))
                .Append(process)
                .ToArray();

            await configStore.SaveAsync(new AppConfig(processes), CancellationToken.None);

            SavedProcess = process;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "保存设置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
