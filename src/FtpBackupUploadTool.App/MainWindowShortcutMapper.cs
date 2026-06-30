using System.Windows.Input;

namespace FtpBackupUploadTool.App;

public enum WindowShortcutAction
{
    None,
    Copy,
    Paste,
    Delete,
    Refresh
}

public static class MainWindowShortcutMapper
{
    public static WindowShortcutAction Resolve(Key key, ModifierKeys modifiers)
    {
        if (modifiers == ModifierKeys.Control)
        {
            return key switch
            {
                Key.C => WindowShortcutAction.Copy,
                Key.V => WindowShortcutAction.Paste,
                _ => WindowShortcutAction.None
            };
        }

        if (modifiers != ModifierKeys.None)
        {
            return WindowShortcutAction.None;
        }

        return key switch
        {
            Key.F8 => WindowShortcutAction.Delete,
            Key.F9 => WindowShortcutAction.Refresh,
            _ => WindowShortcutAction.None
        };
    }
}
