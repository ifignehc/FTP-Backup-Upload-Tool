using System.Collections.ObjectModel;
using System.Windows.Input;
using FtpBackupUploadTool.App.Commands;

namespace FtpBackupUploadTool.App.ViewModels;

public sealed class SettingsViewModel
{
    public SettingsViewModel()
    {
        Processes.Add("默认工序");
        SelectedProcess = "默认工序";
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

    public ObservableCollection<string> Processes { get; } = new();

    public string SelectedProcess { get; set; }

    public string ProcessName { get; set; } = "默认工序";

    public string ProductionHost { get; set; } = "192.168.1.10";

    public string ProductionPort { get; set; } = "21";

    public string ProductionAccount { get; set; } = "prod_user";

    public string DraftHost { get; set; } = "192.168.1.20";

    public string DraftPort { get; set; } = "21";

    public string DraftAccount { get; set; } = "draft_user";

    public string ServerRoot { get; set; } = "/www/project";

    public string LocalRoot { get; set; } = @"D:\Release\project";

    public string BackupDirectory { get; set; } = @"%USERPROFILE%\Desktop";

    public string BackupTemplate { get; set; } = "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup";

    public ObservableCollection<LogFieldItem> LogFields { get; }

    public ICommand SaveCommand { get; }
}

public sealed class LogFieldItem
{
    public LogFieldItem(string name, bool isChecked)
    {
        Name = name;
        IsChecked = isChecked;
    }

    public string Name { get; }

    public bool IsChecked { get; set; }
}
