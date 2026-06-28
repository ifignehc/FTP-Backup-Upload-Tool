using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.Tests;

internal static class ConfigTests
{
    public static void PasswordRoundTripUsesProtector()
    {
        var protector = new InMemoryPasswordProtector();
        var encrypted = protector.Protect("secret");

        TestAssert.True(encrypted != "secret", "protected password should not be plain text");
        TestAssert.Equal("secret", protector.Unprotect(encrypted), "password should decrypt");
    }

    public static void DpapiPasswordRoundTripForCurrentUser()
    {
        var protector = new DpapiPasswordProtector();
        var encrypted = protector.Protect("secret");

        TestAssert.True(encrypted != "secret", "protected password should not be plain text");
        TestAssert.Equal("secret", protector.Unprotect(encrypted), "password should decrypt");
    }

    public static void ConfigRoundTripPreservesProcess()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftp-tool-config", Guid.NewGuid().ToString("N"));
        var store = new AppConfigStore(Path.Combine(dir, "appsettings.json"));
        var config = new AppConfig(new[]
        {
            new ProcessConfig(
                "默认工序",
                new ServerConfig("prod", 21, "prod_user", "enc1", "/www/project"),
                new ServerConfig("draft", 21, "draft_user", "enc2", "/www/project"),
                @"D:\Release\project",
                @".\path-lists\default.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });

        store.SaveAsync(config, CancellationToken.None).GetAwaiter().GetResult();
        var loaded = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("默认工序", loaded.Processes[0].Name, "process name should round trip");
        TestAssert.Equal("prod", loaded.Processes[0].ProductionServer.Host, "host should round trip");
    }

    public static void CanceledSavePreservesExistingConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftp-tool-config", Guid.NewGuid().ToString("N"));
        var store = new AppConfigStore(Path.Combine(dir, "appsettings.json"));
        var original = new AppConfig(new[]
        {
            new ProcessConfig(
                "默认工序",
                new ServerConfig("prod", 21, "prod_user", "enc1", "/www/project"),
                new ServerConfig("draft", 21, "draft_user", "enc2", "/www/project"),
                @"D:\Release\project",
                @".\path-lists\default.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });
        var replacement = new AppConfig(new[]
        {
            new ProcessConfig(
                "取消后的工序",
                new ServerConfig("prod2", 21, "prod_user2", "enc3", "/www/project2"),
                new ServerConfig("draft2", 21, "draft_user2", "enc4", "/www/project2"),
                @"D:\Release\project2",
                @".\path-lists\replacement.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });

        store.SaveAsync(original, CancellationToken.None).GetAwaiter().GetResult();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        try
        {
            store.SaveAsync(replacement, cts.Token).GetAwaiter().GetResult();
            throw new InvalidOperationException("Canceled save should throw.");
        }
        catch (OperationCanceledException)
        {
        }

        var loaded = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("默认工序", loaded.Processes[0].Name, "canceled save should preserve original config");
    }

    private sealed class InMemoryPasswordProtector : IPasswordProtector
    {
        public string Protect(string plainText) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected:{plainText}"));

        public string Unprotect(string protectedText) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText)).Replace("protected:", "", StringComparison.Ordinal);
    }
}
