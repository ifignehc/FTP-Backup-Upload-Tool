using System.Windows;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.App.Views;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel viewModel;

    public MainWindow()
    {
        InitializeComponent();
        viewModel = new MainViewModel();
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
