using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly BackupService backupService;
    private readonly UploadService uploadService;
    private readonly CheckService checkService;
    private string pathListText = string.Empty;
    private string selectedProcess;
    private bool isWorkflowRunning;

    public MainViewModel(BackupService backupService, UploadService uploadService, CheckService checkService)
    {
        this.backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        this.uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        this.checkService = checkService ?? throw new ArgumentNullException(nameof(checkService));

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

        BackupCommand = new RelayCommand(async _ => await RunWorkflowAsync("Backup", RunBackupCoreAsync), _ => !IsWorkflowRunning);
        UploadCommand = new RelayCommand(async _ => await RunWorkflowAsync("Upload", RunUploadCoreAsync), _ => !IsWorkflowRunning);
        CheckCommand = new RelayCommand(async _ => await RunWorkflowAsync("Check", RunCheckCoreAsync), _ => !IsWorkflowRunning);
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

    public bool IsWorkflowRunning
    {
        get => isWorkflowRunning;
        private set
        {
            if (isWorkflowRunning == value)
            {
                return;
            }

            isWorkflowRunning = value;
            OnPropertyChanged(nameof(IsWorkflowRunning));
            RaiseWorkflowCommandCanExecuteChanged();
        }
    }

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

    private IReadOnlyList<RelativePath> ParsePaths() => PathListParser.Parse(PathListText);

    private async Task RunWorkflowAsync(string operation, Func<Task> workflow)
    {
        if (IsWorkflowRunning)
        {
            AddLog($"[Warning] {operation}: 已有操作正在执行");
            return;
        }

        try
        {
            IsWorkflowRunning = true;
            await workflow();
        }
        catch (Exception ex)
        {
            AddErrorLog(operation, ex);
        }
        finally
        {
            IsWorkflowRunning = false;
        }
    }

    private async Task RunBackupCoreAsync()
    {
        var paths = ParsePaths();
        var result = await backupService.RunAsync(
            paths,
            "%USERPROFILE%\\Desktop",
            "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup",
            LogFieldOptions.All,
            CancellationToken.None);

        AppendLogs(result.Logs);
    }

    private async Task RunUploadCoreAsync()
    {
        var paths = ParsePaths();
        var result = await uploadService.RunAsync(paths, CancellationToken.None);
        AppendLogs(result.Logs);
    }

    private async Task RunCheckCoreAsync()
    {
        var paths = ParsePaths();
        var result = await checkService.RunAsync(paths, CancellationToken.None);
        AppendLogs(result.Logs);
    }

    private void AppendLogs(IReadOnlyList<OperationLogEntry> entries)
    {
        foreach (var log in entries)
        {
            var path = log.Path is null ? string.Empty : $" {log.Path.Value}:";
            var error = string.IsNullOrWhiteSpace(log.Error) ? string.Empty : $" ({log.Error})";
            AddLog($"[{log.Level}] {log.Operation}:{path} {log.Message}{error}");
        }
    }

    private void AddErrorLog(string operation, Exception exception)
    {
        AddLog($"[Error] {operation}: {exception.Message}");
    }

    private void RaiseWorkflowCommandCanExecuteChanged()
    {
        ((RelayCommand)BackupCommand).RaiseCanExecuteChanged();
        ((RelayCommand)UploadCommand).RaiseCanExecuteChanged();
        ((RelayCommand)CheckCommand).RaiseCanExecuteChanged();
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
