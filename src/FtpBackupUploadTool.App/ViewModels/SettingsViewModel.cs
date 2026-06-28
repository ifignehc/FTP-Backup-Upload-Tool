using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;

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
            new("相对路径", true),
            new("生产完整路径", true),
            new("起案完整路径", true),
            new("本地完整路径", true),
            new("文件大小", true),
            new("最后修改时间", true),
            new("操作结果", true),
            new("错误信息", true),
            new("备注", true)
        };
        SaveCommand = new RelayCommand(_ => Saved?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Saved;

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

    public ICommand SaveCommand { get; }

    private void SetProperty(ref string field, string value, string propertyName)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class LogFieldItem : INotifyPropertyChanged
{
    private bool isChecked;

    public LogFieldItem(string name, bool isChecked)
    {
        Name = name;
        this.isChecked = isChecked;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

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
