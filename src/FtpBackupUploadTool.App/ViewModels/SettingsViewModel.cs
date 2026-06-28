using System.Collections.ObjectModel;
using System.ComponentModel;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string selectedProcess;
    private string processName = "默认工序";
    private string productionHost = "192.168.1.10";
    private string productionPort = "21";
    private string productionAccount = "prod_user";
    private string draftHost = "192.168.1.20";
    private string draftPort = "21";
    private string draftAccount = "draft_user";
    private string serverRoot = "/www/project";
    private string localRoot = @"D:\Release\project";
    private string backupDirectory = @"%USERPROFILE%\Desktop";
    private string backupTemplate = "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup";

    public SettingsViewModel()
    {
        Processes.Add("默认工序");
        selectedProcess = "默认工序";
        LogFields = new ObservableCollection<LogFieldItem>
        {
            new("相对路径", LogFieldOptions.RelativePath, true),
            new("生产完整路径", LogFieldOptions.ProductionFullPath, true),
            new("起案完整路径", LogFieldOptions.DraftFullPath, true),
            new("本地完整路径", LogFieldOptions.LocalFullPath, true),
            new("文件大小", LogFieldOptions.FileSize, true),
            new("最后修改时间", LogFieldOptions.LastModified, true),
            new("操作结果", LogFieldOptions.Result, true),
            new("错误信息", LogFieldOptions.ErrorMessage, true),
            new("备注", LogFieldOptions.Note, true)
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Processes { get; } = new();

    public string SelectedProcess
    {
        get => selectedProcess;
        set => SetProperty(ref selectedProcess, value, nameof(SelectedProcess));
    }

    public string ProcessName
    {
        get => processName;
        set => SetProperty(ref processName, value, nameof(ProcessName));
    }

    public string ProductionHost
    {
        get => productionHost;
        set => SetProperty(ref productionHost, value, nameof(ProductionHost));
    }

    public string ProductionPort
    {
        get => productionPort;
        set => SetProperty(ref productionPort, value, nameof(ProductionPort));
    }

    public string ProductionAccount
    {
        get => productionAccount;
        set => SetProperty(ref productionAccount, value, nameof(ProductionAccount));
    }

    public string DraftHost
    {
        get => draftHost;
        set => SetProperty(ref draftHost, value, nameof(DraftHost));
    }

    public string DraftPort
    {
        get => draftPort;
        set => SetProperty(ref draftPort, value, nameof(DraftPort));
    }

    public string DraftAccount
    {
        get => draftAccount;
        set => SetProperty(ref draftAccount, value, nameof(DraftAccount));
    }

    public string ServerRoot
    {
        get => serverRoot;
        set => SetProperty(ref serverRoot, value, nameof(ServerRoot));
    }

    public string LocalRoot
    {
        get => localRoot;
        set => SetProperty(ref localRoot, value, nameof(LocalRoot));
    }

    public string BackupDirectory
    {
        get => backupDirectory;
        set => SetProperty(ref backupDirectory, value, nameof(BackupDirectory));
    }

    public string BackupTemplate
    {
        get => backupTemplate;
        set => SetProperty(ref backupTemplate, value, nameof(BackupTemplate));
    }

    public ObservableCollection<LogFieldItem> LogFields { get; }

    public void LoadProcess(ProcessConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!Processes.Contains(config.Name))
        {
            Processes.Add(config.Name);
        }

        SelectedProcess = config.Name;
        ProcessName = config.Name;
        ProductionHost = config.ProductionServer.Host;
        ProductionPort = config.ProductionServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ProductionAccount = config.ProductionServer.UserName;
        DraftHost = config.DraftServer.Host;
        DraftPort = config.DraftServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DraftAccount = config.DraftServer.UserName;
        ServerRoot = config.ProductionServer.RootPath;
        LocalRoot = config.LocalRootPath;
        BackupDirectory = config.Backup.BackupDirectory;
        BackupTemplate = config.Backup.FolderNameTemplate;

        foreach (var field in LogFields)
        {
            field.IsChecked = config.Backup.LogFields.HasFlag(field.Option);
        }
    }

    public ProcessConfig BuildProcessConfig(
        IPasswordProtector passwordProtector,
        string productionPassword,
        string draftPassword,
        ProcessConfig? existingProcess = null)
    {
        ArgumentNullException.ThrowIfNull(passwordProtector);

        var name = RequireText(ProcessName, "工序名称");
        var productionPortNumber = ParsePort(ProductionPort, "生产服务器端口");
        var draftPortNumber = ParsePort(DraftPort, "起案服务器端口");
        var productionEncryptedPassword = ResolveEncryptedPassword(
            passwordProtector,
            productionPassword,
            existingProcess?.ProductionServer.EncryptedPassword,
            "生产服务器密码");
        var draftEncryptedPassword = ResolveEncryptedPassword(
            passwordProtector,
            draftPassword,
            existingProcess?.DraftServer.EncryptedPassword,
            "起案服务器密码");

        return new ProcessConfig(
            name,
            new ServerConfig(
                RequireText(ProductionHost, "生产服务器 IP / 域名"),
                productionPortNumber,
                RequireText(ProductionAccount, "生产服务器账号"),
                productionEncryptedPassword,
                RequireText(ServerRoot, "服务器根目录")),
            new ServerConfig(
                RequireText(DraftHost, "起案服务器 IP / 域名"),
                draftPortNumber,
                RequireText(DraftAccount, "起案服务器账号"),
                draftEncryptedPassword,
                RequireText(ServerRoot, "服务器根目录")),
            RequireText(LocalRoot, "本地根目录"),
            existingProcess?.DefaultPathListFile ?? string.Empty,
            new BackupConfig(
                RequireText(BackupDirectory, "备份保存目录"),
                RequireText(BackupTemplate, "备份文件夹命名模板"),
                SelectedLogFields()));
    }

    private void SetProperty(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private LogFieldOptions SelectedLogFields()
    {
        var selected = LogFieldOptions.None;
        foreach (var field in LogFields.Where(field => field.IsChecked))
        {
            selected |= field.Option;
        }

        return selected;
    }

    private static string RequireText(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{label}不能为空。");
        }

        return value.Trim();
    }

    private static int ParsePort(string value, string label)
    {
        if (!int.TryParse(value, out var port) || port <= 0 || port > 65535)
        {
            throw new InvalidOperationException($"{label}必须是 1 到 65535 之间的数字。");
        }

        return port;
    }

    private static string ResolveEncryptedPassword(
        IPasswordProtector passwordProtector,
        string plainPassword,
        string? existingEncryptedPassword,
        string label)
    {
        if (!string.IsNullOrEmpty(plainPassword))
        {
            return passwordProtector.Protect(plainPassword);
        }

        if (!string.IsNullOrWhiteSpace(existingEncryptedPassword))
        {
            return existingEncryptedPassword;
        }

        throw new InvalidOperationException($"{label}不能为空。");
    }
}

public sealed class LogFieldItem : INotifyPropertyChanged
{
    private bool isChecked;

    public LogFieldItem(string name, LogFieldOptions option, bool isChecked)
    {
        Name = name;
        Option = option;
        this.isChecked = isChecked;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public LogFieldOptions Option { get; }

    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value)
            {
                return;
            }

            isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }
}
