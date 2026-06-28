using System.Windows;
using FtpBackupUploadTool.App.ViewModels;

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
        viewModel.AddLog("设置窗口将在下一步接入");
    }
}
