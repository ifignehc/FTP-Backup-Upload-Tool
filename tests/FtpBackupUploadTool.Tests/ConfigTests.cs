using FtpBackupUploadTool.Core.Config;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;
using System.Security.AccessControl;
using System.Security.Principal;

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

    public static void LoadingLegacyConfigDefaultsFtpServersToPassiveMode()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftp-tool-config", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, "appsettings.json");
        File.WriteAllText(
            configPath,
            """
            {
              "processes": [
                {
                  "name": "legacy",
                  "productionServer": {
                    "host": "prod",
                    "port": 21,
                    "userName": "prod_user",
                    "encryptedPassword": "enc1",
                    "rootPath": "/www/project"
                  },
                  "draftServer": {
                    "host": "draft",
                    "port": 21,
                    "userName": "draft_user",
                    "encryptedPassword": "enc2",
                    "rootPath": "/www/project"
                  },
                  "localRootPath": "D:\\Release\\project",
                  "defaultPathListFile": "",
                  "backup": {
                    "backupDirectory": "%USERPROFILE%\\Desktop",
                    "folderNameTemplate": "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup",
                    "logFields": 511
                  }
                }
              ]
            }
            """);
        var store = new AppConfigStore(configPath);

        var loaded = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.True(loaded.Processes[0].ProductionServer.UsePassive, "legacy production server should default to passive mode");
        TestAssert.True(loaded.Processes[0].DraftServer.UsePassive, "legacy draft server should default to passive mode");
        TestAssert.Equal("%USERPROFILE%\\Desktop", loaded.Processes[0].CheckLog.LogDirectory, "legacy check log directory should default to desktop");
        TestAssert.Equal("{yyyy}{MM}{dd}_{HH}{mm}{ss}_Check", loaded.Processes[0].CheckLog.FileNameTemplate, "legacy check log template should default to timestamped check name");
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

    public static void FailedReplacePreservesExistingConfig()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ftp-tool-config", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "appsettings.json");
        var store = new AppConfigStore(path);
        var original = new AppConfig(new[]
        {
            new ProcessConfig(
                "原始工序",
                new ServerConfig("prod", 21, "prod_user", "enc1", "/www/project"),
                new ServerConfig("draft", 21, "draft_user", "enc2", "/www/project"),
                @"D:\Release\project",
                @".\path-lists\default.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });
        var replacement = new AppConfig(new[]
        {
            new ProcessConfig(
                "新工序",
                new ServerConfig("prod2", 21, "prod_user2", "enc3", "/www/project2"),
                new ServerConfig("draft2", 21, "draft_user2", "enc4", "/www/project2"),
                @"D:\Release\project2",
                @".\path-lists\replacement.txt",
                new BackupConfig("%USERPROFILE%\\Desktop", "{yyyy}{MM}{dd}_{HH}{mm}{ss}_Backup", LogFieldOptions.All))
        });

        store.SaveAsync(original, CancellationToken.None).GetAwaiter().GetResult();

        var denyReadRule = AddDenyReadRule(path);
        var replaceFailed = false;

        try
        {
            store.SaveAsync(replacement, CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (IOException)
        {
            replaceFailed = true;
        }
        catch (UnauthorizedAccessException)
        {
            replaceFailed = true;
        }
        finally
        {
            TryRemoveAccessRule(path, denyReadRule);
        }

        if (!replaceFailed)
        {
            throw new InvalidOperationException("Replacing an unreadable config should throw.");
        }

        var loaded = store.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

        TestAssert.Equal("原始工序", loaded.Processes[0].Name, "failed replace should preserve original config");
    }

    private sealed class InMemoryPasswordProtector : IPasswordProtector
    {
        public string Protect(string plainText) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"protected:{plainText}"));

        public string Unprotect(string protectedText) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(protectedText)).Replace("protected:", "", StringComparison.Ordinal);
    }

    private static FileSystemAccessRule AddDenyReadRule(string path)
    {
        var user = WindowsIdentity.GetCurrent().User ?? throw new InvalidOperationException("Current Windows user SID is unavailable.");
        var rule = new FileSystemAccessRule(user, FileSystemRights.Read, AccessControlType.Deny);
        var fileInfo = new FileInfo(path);
        var security = fileInfo.GetAccessControl();
        security.AddAccessRule(rule);
        fileInfo.SetAccessControl(security);
        return rule;
    }

    private static void TryRemoveAccessRule(string path, FileSystemAccessRule rule)
    {
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            var fileInfo = new FileInfo(path);
            var security = fileInfo.GetAccessControl();
            security.RemoveAccessRuleSpecific(rule);
            fileInfo.SetAccessControl(security);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
