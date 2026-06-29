using System.IO;
using System.Windows;
using FtpBackupUploadTool.App.Controls;
using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.App.Views;
using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Logging;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Paths;
using FtpBackupUploadTool.Core.Remote;
using FtpBackupUploadTool.Core.Security;
using FtpBackupUploadTool.Core.Services;

namespace FtpBackupUploadTool.App;

public partial class MainWindow : Window
{
    private readonly AppConfigStore configStore = new(AppConfigStore.GetDefaultConfigPath());
    private readonly DpapiPasswordProtector passwordProtector = new();
    private readonly MainViewModel viewModel;
    private FilePaneClipboard? filePaneClipboard;

    public MainWindow()
    {
        InitializeComponent();
        var unconfiguredClient = new UnconfiguredRemoteFileClient();

        viewModel = new MainViewModel(
            new BackupService(unconfiguredClient, new BackupLogWriter()),
            new UploadService(unconfiguredClient, string.Empty),
            new CheckService(unconfiguredClient, unconfiguredClient));
        viewModel.SettingsRequested += OnSettingsRequested;
        viewModel.ProcessSelectionRequested += OnProcessSelectionRequested;
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = await configStore.LoadAsync(CancellationToken.None);
            viewModel.ReplaceProcesses(config.Processes);
            var selected = config.Processes.FirstOrDefault();

            if (selected is null)
            {
                viewModel.AddLog("[Warning] 未找到已保存工序，请打开设置完成配置");
                return;
            }

            await LoadProcessRuntimeAsync(selected);
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

                await LoadProcessRuntimeAsync(savedProcess);
                viewModel.ReplaceProcesses((await configStore.LoadAsync(CancellationToken.None)).Processes);
            }
            catch (Exception ex)
            {
                viewModel.AddLog($"[Error] 加载新配置失败：{ex.Message}");
            }
        }
    }

    private async void OnProcessSelectionRequested(object? sender, string processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        try
        {
            var config = await configStore.LoadAsync(CancellationToken.None);
            var selected = config.Processes.FirstOrDefault(process =>
                string.Equals(process.Name, processName, StringComparison.OrdinalIgnoreCase));
            if (selected is null)
            {
                viewModel.AddLog($"[Warning] 未找到工序配置：{processName}");
                return;
            }

            await LoadProcessRuntimeAsync(selected);
        }
        catch (Exception ex)
        {
            viewModel.AddLog($"[Error] 切换工序失败：{ex.Message}");
        }
    }

    private async void OnFilePanePathRefreshRequested(object sender, EventArgs e)
    {
        await viewModel.RefreshFilePanesAsync(CancellationToken.None);
    }

    private async Task LoadProcessRuntimeAsync(ProcessConfig process)
    {
        var factory = new ProcessRuntimeFactory(passwordProtector);
        viewModel.LoadProcess(process, factory.Create(process));
        viewModel.AddLog($"已加载配置：{process.Name}");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await viewModel.RefreshFilePanesAsync(timeout.Token);
    }

    private void OnFilePaneCopyRequested(object sender, IReadOnlyList<FileEntry> files)
    {
        var source = GetPaneKind(sender);
        if (source is null || files.Count == 0)
        {
            return;
        }

        filePaneClipboard = new FilePaneClipboard(source.Value, files.ToArray());
        viewModel.AddLog($"已复制 {files.Count} 个文件，来源：{GetPaneLabel(source.Value)}");
    }

    private async void OnFilePanePasteRequested(object sender, IReadOnlyList<FileEntry> _)
    {
        var target = GetPaneKind(sender);
        if (target is null)
        {
            return;
        }

        if (filePaneClipboard is null)
        {
            viewModel.AddLog("[Warning] 没有可粘贴的文件");
            return;
        }

        await CopyFilesAsync(filePaneClipboard.Source, target.Value, filePaneClipboard.Files);
    }

    private async void OnFilePaneFilesDropped(object sender, FilePaneDropEventArgs e)
    {
        var source = GetPaneKind(e.SourcePane);
        var target = GetPaneKind(sender);
        if (source is null || target is null)
        {
            viewModel.AddLog("[Warning] 无法识别拖拽来源或目标");
            return;
        }

        await CopyFilesAsync(source.Value, target.Value, e.Files);
    }

    private async void OnFilePaneDeleteRequested(object sender, IReadOnlyList<FileEntry> files)
    {
        var target = GetPaneKind(sender);
        if (target is null || files.Count == 0)
        {
            return;
        }

        if (!TryGetConfiguredRuntime(out var process, out var services, "Delete"))
        {
            return;
        }

        try
        {
            foreach (var file in files)
            {
                if (target == FilePaneKind.Draft)
                {
                    await services.DraftClient.DeleteFileAsync(file.Path, CancellationToken.None);
                    viewModel.AddLog($"[Normal] Delete {file.Path.Value}: 起案服务器文件已删除");
                }
                else if (target == FilePaneKind.Local)
                {
                    DeleteLocalFile(process, file.Path);
                    viewModel.AddLog($"[Normal] Delete {file.Path.Value}: 本地文件已删除");
                }
            }

            await viewModel.RefreshFilePanesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            viewModel.AddLog($"[Error] Delete: {ex.Message}");
        }
    }

    private async Task CopyFilesAsync(FilePaneKind source, FilePaneKind target, IReadOnlyList<FileEntry> files)
    {
        if (target == FilePaneKind.Production)
        {
            viewModel.AddLog("[Error] Copy: 生产服务器面板为只读，不能写入文件");
            return;
        }

        if (source == target)
        {
            viewModel.AddLog("[Warning] Copy: 来源和目标相同，已跳过");
            return;
        }

        if (!TryGetConfiguredRuntime(out var process, out var services, "Copy"))
        {
            return;
        }

        try
        {
            foreach (var file in files)
            {
                if (target == FilePaneKind.Draft)
                {
                    await CopyToDraftAsync(process, services, source, file.Path);
                }
                else if (target == FilePaneKind.Local)
                {
                    await CopyToLocalAsync(process, services, source, file.Path);
                }
            }

            await viewModel.RefreshFilePanesAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            viewModel.AddLog($"[Error] Copy: {ex.Message}");
        }
    }

    private async Task CopyToDraftAsync(
        ProcessConfig process,
        WorkflowServices services,
        FilePaneKind source,
        RelativePath path)
    {
        var parent = GetParent(path);
        if (parent is not null && !await services.DraftClient.DirectoryExistsAsync(parent, CancellationToken.None))
        {
            viewModel.AddLog($"[Error] Copy {path.Value}: 起案服务器目标父文件夹不存在");
            return;
        }

        if (source == FilePaneKind.Local)
        {
            await using var localSource = File.OpenRead(ToLocalPath(process, path));
            await services.DraftClient.UploadAsync(path, localSource, CancellationToken.None);
        }
        else
        {
            await CopyRemoteToDraftAsync(GetRemoteClient(services, source), services.DraftClient, path);
        }

        viewModel.AddLog($"[Normal] Copy {path.Value}: 已复制到起案服务器");
    }

    private async Task CopyToLocalAsync(
        ProcessConfig process,
        WorkflowServices services,
        FilePaneKind source,
        RelativePath path)
    {
        if (source == FilePaneKind.Local)
        {
            viewModel.AddLog($"[Warning] Copy {path.Value}: 来源和目标均为本地，已跳过");
            return;
        }

        var destination = ToLocalPath(process, path);
        Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? process.LocalRootPath);
        await DownloadToLocalFileAsync(GetRemoteClient(services, source), path, destination);
        viewModel.AddLog($"[Normal] Copy {path.Value}: 已复制到本地");
    }

    private static async Task CopyRemoteToDraftAsync(
        IRemoteFileClient sourceClient,
        IRemoteFileClient draftClient,
        RelativePath path)
    {
        var tempPath = Path.GetTempFileName();
        try
        {
            await using (var tempDestination = File.Create(tempPath))
            {
                await sourceClient.DownloadAsync(path, tempDestination, CancellationToken.None);
            }

            await using var tempSource = File.OpenRead(tempPath);
            await draftClient.UploadAsync(path, tempSource, CancellationToken.None);
        }
        finally
        {
            TryDeleteTempFile(tempPath);
        }
    }

    private static async Task DownloadToLocalFileAsync(IRemoteFileClient sourceClient, RelativePath path, string destination)
    {
        var directory = Path.GetDirectoryName(destination) ?? Directory.GetCurrentDirectory();
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var tempDestination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await sourceClient.DownloadAsync(path, tempDestination, CancellationToken.None);
                await tempDestination.FlushAsync(CancellationToken.None);
            }

            if (File.Exists(destination))
            {
                File.Replace(tempPath, destination, null);
            }
            else
            {
                File.Move(tempPath, destination);
            }
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    private static void DeleteLocalFile(ProcessConfig process, RelativePath path)
    {
        var fullPath = ToLocalPath(process, path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    private static string ToLocalPath(ProcessConfig process, RelativePath path)
    {
        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(process.LocalRootPath));
        var fullPath = Path.GetFullPath(Path.Combine(root, path.Value.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.Equals(root, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("本地路径超出根目录。");
        }

        return fullPath;
    }

    private static RelativePath? GetParent(RelativePath path)
    {
        var index = path.Value.LastIndexOf('/');
        return index <= 0 ? null : RelativePath.Parse(path.Value[..index]);
    }

    private static IRemoteFileClient GetRemoteClient(WorkflowServices services, FilePaneKind source)
    {
        return source switch
        {
            FilePaneKind.Production => services.ProductionClient,
            FilePaneKind.Draft => services.DraftClient,
            _ => throw new InvalidOperationException("来源不是远程服务器面板。")
        };
    }

    private bool TryGetConfiguredRuntime(
        out ProcessConfig process,
        out WorkflowServices services,
        string operation)
    {
        if (viewModel.CurrentProcess is not null && viewModel.CurrentServices is not null)
        {
            process = viewModel.CurrentProcess;
            services = viewModel.CurrentServices;
            return true;
        }

        process = null!;
        services = null!;
        viewModel.AddLog($"[Error] {operation}: 未加载已保存工序，请先完成配置");
        return false;
    }

    private FilePaneKind? GetPaneKind(object? sender)
    {
        return sender switch
        {
            _ when ReferenceEquals(sender, ProductionFilePane) => FilePaneKind.Production,
            _ when ReferenceEquals(sender, DraftFilePane) => FilePaneKind.Draft,
            _ when ReferenceEquals(sender, LocalFilePane) => FilePaneKind.Local,
            _ => null
        };
    }

    private static string GetPaneLabel(FilePaneKind pane)
    {
        return pane switch
        {
            FilePaneKind.Production => "生产服务器",
            FilePaneKind.Draft => "起案服务器",
            FilePaneKind.Local => "本地文件",
            _ => pane.ToString()
        };
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }
}

internal enum FilePaneKind
{
    Production,
    Draft,
    Local
}

internal sealed record FilePaneClipboard(FilePaneKind Source, IReadOnlyList<FileEntry> Files);

internal sealed class UnconfiguredRemoteFileClient : IRemoteFileClient
{
    public Task<IReadOnlyList<FileEntry>> ListDirectoryAsync(RelativePath? directory, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<IReadOnlyList<FileEntry>> ListRecursiveAsync(CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<bool> FileExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<bool> DirectoryExistsAsync(RelativePath path, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task<FileEntry?> GetFileEntryAsync(RelativePath path, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task DownloadAsync(RelativePath path, Stream destination, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task UploadAsync(RelativePath path, Stream source, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    public Task DeleteFileAsync(RelativePath path, CancellationToken cancellationToken)
    {
        throw CreateException();
    }

    private static InvalidOperationException CreateException()
    {
        return new InvalidOperationException("未加载已保存工序，请先完成配置。");
    }
}
