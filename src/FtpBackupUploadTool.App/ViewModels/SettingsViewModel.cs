using System.Collections.ObjectModel;
using System.ComponentModel;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private const string NewProcessName = "新工序";
    private const string CopySuffix = " 副本";

    private readonly Dictionary<string, ProcessConfig> savedProcesses = new(StringComparer.OrdinalIgnoreCase);
    private bool isLoadingProcess;
    private string selectedProcess;
    private string processName = "默认工序";
    private string productionHost = "192.168.1.10";
    private string productionPort = "21";
    private string productionAccount = "prod_user";
    private string productionPasswordHint = "未保存密码，请输入";
    private bool rememberProductionPassword;
    private bool productionUsePassive = true;
    private string draftHost = "192.168.1.20";
    private string draftPort = "21";
    private string draftAccount = "draft_user";
    private string draftPasswordHint = "未保存密码，请输入";
    private bool rememberDraftPassword;
    private bool draftUsePassive = true;
    private string serverRoot = "/www/project";
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

    public event EventHandler? ProcessLoaded;

    public ObservableCollection<string> Processes { get; } = new();

    public string SelectedProcess
    {
        get => selectedProcess;
        set
        {
            if (!SetProperty(ref selectedProcess, value, nameof(SelectedProcess)))
            {
                return;
            }

            if (!isLoadingProcess && savedProcesses.TryGetValue(selectedProcess, out var config))
            {
                LoadProcess(config);
            }
        }
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

    public string ProductionPasswordHint
    {
        get => productionPasswordHint;
        set => SetProperty(ref productionPasswordHint, value, nameof(ProductionPasswordHint));
    }

    public bool RememberProductionPassword
    {
        get => rememberProductionPassword;
        set => SetProperty(ref rememberProductionPassword, value, nameof(RememberProductionPassword));
    }

    public bool ProductionUsePassive
    {
        get => productionUsePassive;
        set => SetProperty(ref productionUsePassive, value, nameof(ProductionUsePassive));
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

    public string DraftPasswordHint
    {
        get => draftPasswordHint;
        set => SetProperty(ref draftPasswordHint, value, nameof(DraftPasswordHint));
    }

    public bool RememberDraftPassword
    {
        get => rememberDraftPassword;
        set => SetProperty(ref rememberDraftPassword, value, nameof(RememberDraftPassword));
    }

    public bool DraftUsePassive
    {
        get => draftUsePassive;
        set => SetProperty(ref draftUsePassive, value, nameof(DraftUsePassive));
    }

    public string ServerRoot
    {
        get => serverRoot;
        set => SetProperty(ref serverRoot, value, nameof(ServerRoot));
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

    public void LoadProcesses(IEnumerable<ProcessConfig> configs, string? selectedName)
    {
        ArgumentNullException.ThrowIfNull(configs);

        savedProcesses.Clear();
        Processes.Clear();

        foreach (var config in configs.Where(config => !string.IsNullOrWhiteSpace(config.Name)))
        {
            savedProcesses[config.Name] = config;
            if (!Processes.Contains(config.Name))
            {
                Processes.Add(config.Name);
            }
        }

        var selected = !string.IsNullOrWhiteSpace(selectedName)
            && savedProcesses.TryGetValue(selectedName, out var selectedConfig)
                ? selectedConfig
                : savedProcesses.Values.FirstOrDefault();

        if (selected is not null)
        {
            LoadProcess(selected);
            return;
        }

        Processes.Add("默认工序");
        SelectedProcess = "默认工序";
    }

    public void LoadProcess(ProcessConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        savedProcesses[config.Name] = config;
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

        ProcessName = config.Name;
        ProductionHost = config.ProductionServer.Host;
        ProductionPort = config.ProductionServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        ProductionAccount = config.ProductionServer.UserName;
        ProductionPasswordHint = GetPasswordHint(config.ProductionServer.EncryptedPassword);
        RememberProductionPassword = HasSavedPassword(config.ProductionServer.EncryptedPassword);
        ProductionUsePassive = config.ProductionServer.UsePassive;
        DraftHost = config.DraftServer.Host;
        DraftPort = config.DraftServer.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        DraftAccount = config.DraftServer.UserName;
        DraftPasswordHint = GetPasswordHint(config.DraftServer.EncryptedPassword);
        RememberDraftPassword = HasSavedPassword(config.DraftServer.EncryptedPassword);
        DraftUsePassive = config.DraftServer.UsePassive;
        ServerRoot = config.ProductionServer.RootPath;
        BackupDirectory = config.Backup.BackupDirectory;
        BackupTemplate = config.Backup.FolderNameTemplate;

        foreach (var field in LogFields)
        {
            field.IsChecked = config.Backup.LogFields.HasFlag(field.Option);
        }

        ProcessLoaded?.Invoke(this, EventArgs.Empty);
    }

    public void AddProcess()
    {
        var name = CreateUniqueProcessName(NewProcessName);
        Processes.Add(name);
        SelectDraftProcess(name);
        ResetProcessFields(name);
    }

    public void CopySelectedProcess()
    {
        if (string.IsNullOrWhiteSpace(SelectedProcess))
        {
            return;
        }

        var baseName = string.IsNullOrWhiteSpace(ProcessName) ? SelectedProcess : ProcessName.Trim();
        var name = CreateUniqueProcessName($"{baseName}{CopySuffix}");
        Processes.Add(name);
        SelectDraftProcess(name);
        ProcessName = name;
    }

    public bool DeleteSelectedProcess()
    {
        if (string.IsNullOrWhiteSpace(SelectedProcess))
        {
            return false;
        }

        var selectedName = SelectedProcess;
        var selectedIndex = FindProcessIndex(selectedName);
        if (selectedIndex < 0)
        {
            return false;
        }

        Processes.RemoveAt(selectedIndex);
        savedProcesses.Remove(selectedName);

        if (Processes.Count == 0)
        {
            var name = CreateUniqueProcessName(NewProcessName);
            Processes.Add(name);
            SelectDraftProcess(name);
            ResetProcessFields(name);
            return true;
        }

        var nextIndex = Math.Min(selectedIndex, Processes.Count - 1);
        var nextName = Processes[nextIndex];
        if (savedProcesses.TryGetValue(nextName, out var nextConfig))
        {
            LoadProcess(nextConfig);
        }
        else
        {
            SelectDraftProcess(nextName);
            ProcessName = nextName;
        }

        return true;
    }

    public bool HasSavedSelectedProcess()
    {
        return savedProcesses.ContainsKey(SelectedProcess);
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
            RememberProductionPassword,
            "生产服务器密码");
        var draftEncryptedPassword = ResolveEncryptedPassword(
            passwordProtector,
            draftPassword,
            existingProcess?.DraftServer.EncryptedPassword,
            RememberDraftPassword,
            "起案服务器密码");

        return new ProcessConfig(
            name,
            new ServerConfig(
                RequireText(ProductionHost, "生产服务器 IP / 域名"),
                productionPortNumber,
                RequireText(ProductionAccount, "生产服务器账号"),
                productionEncryptedPassword,
                RequireText(ServerRoot, "服务器根目录"),
                ProductionUsePassive),
            new ServerConfig(
                RequireText(DraftHost, "起案服务器 IP / 域名"),
                draftPortNumber,
                RequireText(DraftAccount, "起案服务器账号"),
                draftEncryptedPassword,
                RequireText(ServerRoot, "服务器根目录"),
                DraftUsePassive),
            existingProcess?.LocalRootPath ?? string.Empty,
            existingProcess?.DefaultPathListFile ?? string.Empty,
            new BackupConfig(
                RequireText(BackupDirectory, "备份保存目录"),
                RequireText(BackupTemplate, "备份文件夹命名模板"),
                SelectedLogFields()));
    }

    public string GetProductionPasswordForDisplay(IPasswordProtector passwordProtector)
    {
        ArgumentNullException.ThrowIfNull(passwordProtector);

        var encryptedPassword = GetSelectedProcess()?.ProductionServer.EncryptedPassword;
        return GetPasswordForDisplay(passwordProtector, encryptedPassword, RememberProductionPassword);
    }

    public string GetDraftPasswordForDisplay(IPasswordProtector passwordProtector)
    {
        ArgumentNullException.ThrowIfNull(passwordProtector);

        var encryptedPassword = GetSelectedProcess()?.DraftServer.EncryptedPassword;
        return GetPasswordForDisplay(passwordProtector, encryptedPassword, RememberDraftPassword);
    }

    private void SelectDraftProcess(string name)
    {
        isLoadingProcess = true;
        try
        {
            SelectedProcess = name;
        }
        finally
        {
            isLoadingProcess = false;
        }
    }

    private void ResetProcessFields(string name)
    {
        ProcessName = name;
        ProductionHost = "192.168.1.10";
        ProductionPort = "21";
        ProductionAccount = "prod_user";
        ProductionPasswordHint = GetPasswordHint(string.Empty);
        RememberProductionPassword = false;
        ProductionUsePassive = true;
        DraftHost = "192.168.1.20";
        DraftPort = "21";
        DraftAccount = "draft_user";
        DraftPasswordHint = GetPasswordHint(string.Empty);
        RememberDraftPassword = false;
        DraftUsePassive = true;
        ServerRoot = "/www/project";
        BackupDirectory = @"%USERPROFILE%\Desktop";
        BackupTemplate = "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup";

        foreach (var field in LogFields)
        {
            field.IsChecked = true;
        }

        ProcessLoaded?.Invoke(this, EventArgs.Empty);
    }

    private string CreateUniqueProcessName(string baseName)
    {
        var normalizedBaseName = string.IsNullOrWhiteSpace(baseName) ? NewProcessName : baseName.Trim();
        var candidate = normalizedBaseName;
        var index = 2;

        while (ContainsProcessName(candidate))
        {
            candidate = $"{normalizedBaseName} {index}";
            index++;
        }

        return candidate;
    }

    private bool ContainsProcessName(string name)
    {
        return Processes.Any(process => string.Equals(process, name, StringComparison.OrdinalIgnoreCase));
    }

    private int FindProcessIndex(string name)
    {
        for (var index = 0; index < Processes.Count; index++)
        {
            if (string.Equals(Processes[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private bool SetProperty(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private bool SetProperty(ref bool field, bool value, string propertyName)
    {
        if (field == value)
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
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
        bool rememberPassword,
        string label)
    {
        if (!string.IsNullOrEmpty(plainPassword))
        {
            return passwordProtector.Protect(plainPassword);
        }

        if (!rememberPassword)
        {
            return string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(existingEncryptedPassword))
        {
            return existingEncryptedPassword;
        }

        throw new InvalidOperationException($"{label}未保存，请输入密码。");
    }

    private static string GetPasswordHint(string encryptedPassword)
    {
        return string.IsNullOrWhiteSpace(encryptedPassword)
            ? "未保存密码，请输入"
            : "已保存并会自动填入；输入新密码则替换";
    }

    private ProcessConfig? GetSelectedProcess()
    {
        return savedProcesses.TryGetValue(SelectedProcess, out var config) ? config : null;
    }

    private static string GetPasswordForDisplay(
        IPasswordProtector passwordProtector,
        string? encryptedPassword,
        bool rememberPassword)
    {
        if (!rememberPassword || string.IsNullOrWhiteSpace(encryptedPassword))
        {
            return string.Empty;
        }

        return passwordProtector.Unprotect(encryptedPassword);
    }

    private static bool HasSavedPassword(string encryptedPassword)
    {
        return !string.IsNullOrWhiteSpace(encryptedPassword);
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
