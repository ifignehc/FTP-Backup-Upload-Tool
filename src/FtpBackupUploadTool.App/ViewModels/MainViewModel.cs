using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.Core.Logging;
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
    private readonly CheckLogWriter checkLogWriter = new();
    private ProcessConfig? currentProcess;
    private WorkflowServices? currentServices;
    private string pathListText = string.Empty;
    private string rootSummary = string.Empty;
    private string selectedProcess;
    private bool isWorkflowRunning;
    private bool isLoadingProcess;

    public MainViewModel(BackupService backupService, UploadService uploadService, CheckService checkService)
    {
        this.backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        this.uploadService = uploadService ?? throw new ArgumentNullException(nameof(uploadService));
        this.checkService = checkService ?? throw new ArgumentNullException(nameof(checkService));

        Processes = new ObservableCollection<string>();
        selectedProcess = string.Empty;
        RootSummary = "根目录：生产 / 起案 / 本地";
        PathListText = string.Empty;

        ProductionPane = new FilePaneViewModel("生产服务器", true, Array.Empty<FileEntry>());
        DraftPane = new FilePaneViewModel("起案服务器", false, Array.Empty<FileEntry>());
        LocalPane = new FilePaneViewModel("本地文件", false, Array.Empty<FileEntry>(), usesAbsolutePaths: true);
        BackupPane = new FilePaneViewModel("备份 / 对照", false, Array.Empty<FileEntry>(), usesAbsolutePaths: true);
        Logs = new ObservableCollection<string>
        {
            FormatLog("工具界面已就绪")
        };

        BackupCommand = new RelayCommand(async _ => await RunWorkflowAsync("Backup", RunBackupCoreAsync), _ => CanRunWorkflow());
        UploadCommand = new RelayCommand(async _ => await RunWorkflowAsync("Upload", RunUploadCoreAsync), _ => CanRunWorkflow());
        CheckCommand = new RelayCommand(async _ => await RunWorkflowAsync("Check", RunCheckCoreAsync), _ => CanRunWorkflow());
        OpenSettingsCommand = new RelayCommand(_ => SettingsRequested?.Invoke(this, EventArgs.Empty));
        ConsolidatePathListCommand = new RelayCommand(_ => ConsolidatePathList());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? SettingsRequested;

    public event EventHandler<string>? ProcessSelectionRequested;

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
            if (!string.IsNullOrWhiteSpace(selectedProcess))
            {
                AddLog($"已切换工序：{selectedProcess}");
            }

            if (!isLoadingProcess)
            {
                ProcessSelectionRequested?.Invoke(this, selectedProcess);
            }
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
            OnPropertyChanged(nameof(PathListCountDisplay));
            RaiseWorkflowCommandCanExecuteChanged();
        }
    }

    public string PathListCountDisplay => $"{ParsePathsForDisplay().Count} 个路径";

    public FilePaneViewModel ProductionPane { get; }

    public FilePaneViewModel DraftPane { get; }

    public FilePaneViewModel LocalPane { get; }

    public FilePaneViewModel BackupPane { get; }

    public ObservableCollection<string> Logs { get; }

    public ProcessConfig? CurrentProcess => currentProcess;

    public WorkflowServices? CurrentServices => currentServices;

    public ICommand BackupCommand { get; }

    public ICommand UploadCommand { get; }

    public ICommand CheckCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand ConsolidatePathListCommand { get; }

    public void AddLog(string message)
    {
        Logs.Add(FormatLog(message));
    }

    public void ReplaceProcesses(IEnumerable<ProcessConfig> configs)
    {
        var names = configs
            .Select(config => config.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Processes.Clear();
        foreach (var name in names)
        {
            Processes.Add(name);
        }
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

        isLoadingProcess = true;
        try
        {
            SelectedProcess = config.Name;
        }
        finally
        {
            isLoadingProcess = false;
        }

        ProductionPane.CurrentPath = "/";
        DraftPane.CurrentPath = "/";
        BackupPane.CurrentPath = config.Backup.BackupDirectory;
        RootSummary = $"服务器根目录：{config.ProductionServer.RootPath}";
    }

    public async Task RefreshFilePanesAsync(CancellationToken cancellationToken)
    {
        if (currentProcess is null || currentServices is null)
        {
            ProductionPane.ReplaceFiles(Array.Empty<FileEntry>());
            DraftPane.ReplaceFiles(Array.Empty<FileEntry>());
            LocalPane.ReplaceFiles(Array.Empty<FileEntry>());
            BackupPane.ReplaceFiles(Array.Empty<FileEntry>());
            return;
        }

        await RefreshRemotePaneAsync("生产服务器", ProductionPane, currentServices.ProductionClient, cancellationToken);
        await RefreshRemotePaneAsync("起案服务器", DraftPane, currentServices.DraftClient, cancellationToken);
        RefreshLocalPane(LocalPane, GetCurrentLocalRoot(), "本地");
        RefreshLocalPane(BackupPane, BackupPane.CurrentPath, "备份");
    }

    public async Task RefreshFilePaneAsync(FilePaneViewModel pane, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pane);

        if (currentProcess is null || currentServices is null)
        {
            pane.ReplaceFiles(Array.Empty<FileEntry>());
            return;
        }

        if (ReferenceEquals(pane, ProductionPane))
        {
            await RefreshRemotePaneAsync("生产服务器", ProductionPane, currentServices.ProductionClient, cancellationToken);
        }
        else if (ReferenceEquals(pane, DraftPane))
        {
            await RefreshRemotePaneAsync("起案服务器", DraftPane, currentServices.DraftClient, cancellationToken);
        }
        else if (ReferenceEquals(pane, LocalPane))
        {
            RefreshLocalPane(LocalPane, GetCurrentLocalRoot(), "本地");
        }
        else if (ReferenceEquals(pane, BackupPane))
        {
            RefreshLocalPane(BackupPane, BackupPane.CurrentPath, "备份");
        }
    }

    private IReadOnlyList<RelativePath> ParsePaths() => PathListParser.Parse(PathListText);

    private IReadOnlyList<RelativePath> ParsePathsForDisplay()
    {
        try
        {
            return ParsePaths();
        }
        catch (ArgumentException)
        {
            return Array.Empty<RelativePath>();
        }
    }

    private void ConsolidatePathList()
    {
        try
        {
            PathListText = string.Join("\r\n", ParsePaths().Select(path => path.Value));
        }
        catch (ArgumentException ex)
        {
            AddLog($"[Warning] 路径清单整理失败：{ex.Message}");
        }
    }

    private bool CanRunWorkflow() => !IsWorkflowRunning && HasPathListEntries();

    private bool HasPathListEntries()
    {
        return PathListText
            .Split(new[] { "\r\n", "\n", "," }, StringSplitOptions.None)
            .Any(line => line.Trim().Trim(',').Length > 0);
    }

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
        var result = await uploadService.RunAsync(paths, GetCurrentLocalRoot(), CancellationToken.None);
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
        var result = await checkService.RunAsync(paths, GetCurrentLocalRoot(), CancellationToken.None);
        AppendLogs(result.Logs);
        var logPath = await checkLogWriter.WriteAsync(
            currentProcess.CheckLog.LogDirectory,
            currentProcess.CheckLog.FileNameTemplate,
            result.Rows,
            CancellationToken.None);
        AddLog($"检查日志已保存：{logPath}");
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
            pane.ReplaceFiles(await client.ListDirectoryAsync(ToRelativePath(pane.CurrentPath), cancellationToken));
        }
        catch (Exception ex)
        {
            pane.ReplaceFiles(Array.Empty<FileEntry>());
            AddLog($"[Warning] {label}文件列表刷新失败：{ex.Message}");
        }
    }

    private void RefreshLocalPane(FilePaneViewModel pane, string directoryPath, string label)
    {
        try
        {
            var currentDirectory = Path.GetFullPath(Environment.ExpandEnvironmentVariables(directoryPath));
            if (!Directory.Exists(currentDirectory))
            {
                pane.ReplaceFiles(Array.Empty<FileEntry>());
                AddLog($"[Warning] {label}路径不存在：{currentDirectory}");
                return;
            }

            var directories = Directory.EnumerateDirectories(currentDirectory)
                .Select(directory =>
                {
                    return new FileEntry(CreateLocalPaneEntryPath(directory), true, 0, Directory.GetLastWriteTimeUtc(directory));
                });
            var files = Directory.EnumerateFiles(currentDirectory)
                .Select(file =>
                {
                    var info = new FileInfo(file);
                    return new FileEntry(CreateLocalPaneEntryPath(file), false, info.Length, info.LastWriteTimeUtc);
                });
            pane.ReplaceFiles(directories.Concat(files));
        }
        catch (Exception ex)
        {
            pane.ReplaceFiles(Array.Empty<FileEntry>());
            AddLog($"[Warning] {label}文件列表刷新失败：{ex.Message}");
        }
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static RelativePath? ToRelativePath(string path)
    {
        var normalized = FilePaneViewModel.NormalizePath(path).Trim('/');
        return normalized.Length == 0 ? null : RelativePath.Parse(normalized);
    }

    public string GetCurrentLocalRoot()
    {
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(LocalPane.CurrentPath));
    }

    public string GetCurrentBackupRoot()
    {
        return Path.GetFullPath(Environment.ExpandEnvironmentVariables(BackupPane.CurrentPath));
    }

    private static RelativePath CreateLocalPaneEntryPath(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        return RelativePath.Parse(name);
    }
}
