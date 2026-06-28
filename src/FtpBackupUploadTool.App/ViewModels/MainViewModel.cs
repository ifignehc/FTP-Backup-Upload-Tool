using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private string pathListText = string.Empty;
    private string selectedProcess;

    public MainViewModel()
    {
        Processes = new ObservableCollection<string>
        {
            "默认工序",
            "紧急发布",
            "常规更新"
        };
        selectedProcess = Processes[0];
        RootSummary = "根目录：生产 / 起案 / 本地";
        PathListText = "css/site.css\r\nimages/logo.png\r\nscripts/app.js";

        ProductionPane = new FilePaneViewModel("生产服务器", true, CreateSampleFiles());
        DraftPane = new FilePaneViewModel("起案服务器", false, CreateSampleFiles());
        LocalPane = new FilePaneViewModel("本地文件", false, CreateSampleFiles());
        Logs = new ObservableCollection<string>
        {
            FormatLog("工具界面已就绪")
        };

        BackupCommand = new RelayCommand(_ => AddLog("备份按钮已点击"));
        UploadCommand = new RelayCommand(_ => AddLog("上传按钮已点击"));
        CheckCommand = new RelayCommand(_ => AddLog("Check按钮已点击"));
        OpenSettingsCommand = new RelayCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SettingsRequested;

    public ObservableCollection<string> Processes { get; }

    public string SelectedProcess
    {
        get => selectedProcess;
        set
        {
            if (selectedProcess == value)
            {
                return;
            }

            selectedProcess = value;
            OnPropertyChanged(nameof(SelectedProcess));
            AddLog($"已切换工序：{selectedProcess}");
        }
    }

    public string RootSummary { get; }

    public string PathListText
    {
        get => pathListText;
        set
        {
            if (pathListText == value)
            {
                return;
            }

            pathListText = value;
            OnPropertyChanged(nameof(PathListText));
        }
    }

    public FilePaneViewModel ProductionPane { get; }

    public FilePaneViewModel DraftPane { get; }

    public FilePaneViewModel LocalPane { get; }

    public ObservableCollection<string> Logs { get; }

    public ICommand BackupCommand { get; }

    public ICommand UploadCommand { get; }

    public ICommand CheckCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public void AddLog(string message)
    {
        Logs.Add(FormatLog(message));
    }

    private static string FormatLog(string message) => $"{DateTime.Now:HH:mm:ss}  {message}";

    private static FileEntry[] CreateSampleFiles() =>
    [
        new FileEntry(RelativePath.Parse("css/site.css"), false, 12840, new DateTimeOffset(2026, 6, 28, 9, 30, 0, TimeSpan.Zero)),
        new FileEntry(RelativePath.Parse("images/logo.png"), false, 48216, new DateTimeOffset(2026, 6, 28, 9, 42, 0, TimeSpan.Zero)),
        new FileEntry(RelativePath.Parse("scripts/app.js"), false, 9344, new DateTimeOffset(2026, 6, 28, 10, 15, 0, TimeSpan.Zero))
    ];

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
