using System.Windows;
using FtpBackupUploadTool.App.ViewModels;

namespace FtpBackupUploadTool.App.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel viewModel = new();

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Saved += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
