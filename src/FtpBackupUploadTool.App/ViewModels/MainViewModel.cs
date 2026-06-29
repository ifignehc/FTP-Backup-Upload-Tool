using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private BackupService backupService;
    private UploadService uploadService;
    private CheckService checkService;
    private ProcessConfig? currentProcess;
    private WorkflowServices? currentServices;
    private string pathListText = string.Empty;
    private string rootSummary = string.Empty;
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

        ProductionPane = new FilePaneViewModel("生产服务器", true, Array.Empty<FileEntry>());
        DraftPane = new FilePaneViewModel("起案服务器", false, Array.Empty<FileEntry>());
        LocalPane = new FilePaneViewModel("本地文件", false, Array.Empty<FileEntry>());
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

    public string RootSummary
    {
        get => rootSummary;
        set
        {
            if (rootSummary == value)
            {
                return;
            }

            rootSummary = value;
            OnPropertyChanged(nameof(RootSummary));
        }
    }

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

    public ProcessConfig? CurrentProcess => currentProcess;

    public WorkflowServices? CurrentServices => currentServices;

    public ICommand BackupCommand { get; }

    public ICommand UploadCommand { get; }

    public ICommand CheckCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public void AddLog(string message)
    {
        Logs.Add(FormatLog(message));
    }

    public void LoadProcess(ProcessConfig config, WorkflowServices services)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(services);

        currentProcess = config;
        currentServices = services;
        backupService = services.BackupService;
        uploadService = services.UploadService;
        checkService = services.CheckService;

        if (!Processes.Contains(config.Name))
        {
            Processes.Add(config.Name);
        }

        SelectedProcess = config.Name;
        RootSummary = $"根目录一致：{config.ProductionServer.RootPath} | 本地：{config.LocalRootPath}";
    }

    public async Task RefreshFilePanesAsync(CancellationToken cancellationToken)
    {
        if (currentProcess is null || currentServices is null)
        {
            ProductionPane.ReplaceFiles(Array.Empty<FileEntry>());
            DraftPane.ReplaceFiles(Array.Empty<FileEntry>());
            LocalPane.ReplaceFiles(Array.Empty<FileEntry>());
            return;
        }

        await RefreshRemotePaneAsync("生产服务器", ProductionPane, currentServices.ProductionClient, cancellationToken);
        await RefreshRemotePaneAsync("起案服务器", DraftPane, currentServices.DraftClient, cancellationToken);
        RefreshLocalPane();
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
        if (currentProcess is null)
        {
            AddLog("[Error] Backup: 未加载已保存工序，请先完成配置");
            return;
        }

        var paths = ParsePaths();
        var result = await backupService.RunAsync(
            paths,
            currentProcess.Backup.BackupDirectory,
            currentProcess.Backup.FolderNameTemplate,
            currentProcess.Backup.LogFields,
            CancellationToken.None,
            currentProcess.ProductionServer.RootPath,
            currentProcess.DraftServer.RootPath);

        AppendLogs(result.Logs);
        await RefreshFilePanesAsync(CancellationToken.None);
    }

    private async Task RunUploadCoreAsync()
    {
        if (currentProcess is null)
        {
            AddLog("[Error] Upload: 未加载已保存工序，请先完成配置");
            return;
        }

        var paths = ParsePaths();
        var result = await uploadService.RunAsync(paths, CancellationToken.None);
        AppendLogs(result.Logs);
        await RefreshFilePanesAsync(CancellationToken.None);
    }

    private async Task RunCheckCoreAsync()
    {
        if (currentProcess is null)
        {
            AddLog("[Error] Check: 未加载已保存工序，请先完成配置");
            return;
        }

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

    private async Task RefreshRemotePaneAsync(
        string label,
        FilePaneViewModel pane,
        IRemoteFileClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            pane.ReplaceFiles(await client.ListRecursiveAsync(cancellationToken));
        }
        catch (Exception ex)
        {
            pane.ReplaceFiles(Array.Empty<FileEntry>());
            AddLog($"[Warning] {label}文件列表刷新失败：{ex.Message}");
        }
    }

    private void RefreshLocalPane()
    {
        if (currentProcess is null)
        {
            LocalPane.ReplaceFiles(Array.Empty<FileEntry>());
            return;
        }

        try
        {
            var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(currentProcess.LocalRootPath));
            if (!Directory.Exists(root))
            {
                LocalPane.ReplaceFiles(Array.Empty<FileEntry>());
                AddLog($"[Warning] 本地根目录不存在：{root}");
                return;
            }

            var files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(file =>
                {
                    var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    var info = new FileInfo(file);
                    return new FileEntry(RelativePath.Parse(relative), false, info.Length, info.LastWriteTimeUtc);
                })
                .ToArray();
            LocalPane.ReplaceFiles(files);
        }
        catch (Exception ex)
        {
            LocalPane.ReplaceFiles(Array.Empty<FileEntry>());
            AddLog($"[Warning] 本地文件列表刷新失败：{ex.Message}");
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
