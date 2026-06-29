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
        viewModel.ProcessLoaded += OnProcessLoaded;
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

    private void OnProcessLoaded(object? sender, EventArgs e)
    {
        PopulatePasswordBoxesFromSavedPasswords();
    }

    private void OnRememberPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender == RememberProductionPasswordCheckBox)
        {
            if (RememberProductionPasswordCheckBox.IsChecked == true)
            {
                SetProductionPasswordBoxFromSavedPassword();
            }
            else
            {
                ProductionPasswordBox.Password = string.Empty;
            }
        }

        if (sender == RememberDraftPasswordCheckBox)
        {
            if (RememberDraftPasswordCheckBox.IsChecked == true)
            {
                SetDraftPasswordBoxFromSavedPassword();
            }
            else
            {
                DraftPasswordBox.Password = string.Empty;
            }
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var existingConfig = await configStore.LoadAsync(CancellationToken.None);
            var existingProcess = existingConfig.Processes.FirstOrDefault(process =>
                string.Equals(process.Name, viewModel.SelectedProcess, StringComparison.OrdinalIgnoreCase));
            viewModel.RememberProductionPassword = RememberProductionPasswordCheckBox.IsChecked == true;
            viewModel.RememberDraftPassword = RememberDraftPasswordCheckBox.IsChecked == true;
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

    private void PopulatePasswordBoxesFromSavedPasswords()
    {
        SetProductionPasswordBoxFromSavedPassword();
        SetDraftPasswordBoxFromSavedPassword();
    }

    private void SetProductionPasswordBoxFromSavedPassword()
    {
        try
        {
            ProductionPasswordBox.Password = viewModel.GetProductionPasswordForDisplay(passwordProtector);
        }
        catch (Exception ex)
        {
            ProductionPasswordBox.Password = string.Empty;
            MessageBox.Show($"生产服务器已保存的密码无法解密，请重新输入。\n{ex.Message}", "读取密码失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetDraftPasswordBoxFromSavedPassword()
    {
        try
        {
            DraftPasswordBox.Password = viewModel.GetDraftPasswordForDisplay(passwordProtector);
        }
        catch (Exception ex)
        {
            DraftPasswordBox.Password = string.Empty;
            MessageBox.Show($"起案服务器已保存的密码无法解密，请重新输入。\n{ex.Message}", "读取密码失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
