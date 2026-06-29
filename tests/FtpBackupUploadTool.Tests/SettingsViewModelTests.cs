using FtpBackupUploadTool.App.ViewModels;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.Tests;

internal static class SettingsViewModelTests
{
    public static void LoadsAllSavedProcessesAndSelectedProcessFields()
    {
        var first = CreateProcess("常规更新", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var second = CreateProcess("紧急发布", "prod-b", "draft-b", "enc-prod-b", "enc-draft-b");
        var viewModel = new SettingsViewModel();

        viewModel.LoadProcesses(new[] { first, second }, "紧急发布");

        TestAssert.Equal(2, viewModel.Processes.Count, "settings process list should contain saved processes");
        TestAssert.Equal("紧急发布", viewModel.SelectedProcess, "selected process should match requested process");
        TestAssert.Equal("prod-b", viewModel.ProductionHost, "selected production host should load");
        TestAssert.Equal("draft-b", viewModel.DraftHost, "selected draft host should load");
    }

    public static void SelectingSavedProcessLoadsItsServerFields()
    {
        var first = CreateProcess("常规更新", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var second = CreateProcess("紧急发布", "prod-b", "draft-b", "enc-prod-b", "enc-draft-b");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { first, second }, "常规更新");

        viewModel.SelectedProcess = "紧急发布";

        TestAssert.Equal("prod-b", viewModel.ProductionHost, "changed selection should load production host");
        TestAssert.Equal("draft-b", viewModel.DraftHost, "changed selection should load draft host");
    }

    public static void EmptyPasswordKeepsExistingSavedPassword()
    {
        var existing = CreateProcess("常规更新", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { existing }, "常规更新");

        var saved = viewModel.BuildProcessConfig(new TestPasswordProtector(), string.Empty, string.Empty, existing);

        TestAssert.Equal("enc-prod-a", saved.ProductionServer.EncryptedPassword, "empty production password should keep saved password");
        TestAssert.Equal("enc-draft-a", saved.DraftServer.EncryptedPassword, "empty draft password should keep saved password");
    }

    private static ProcessConfig CreateProcess(
        string name,
        string productionHost,
        string draftHost,
        string productionPassword,
        string draftPassword)
    {
        return new ProcessConfig(
            name,
            new ServerConfig(productionHost, 21, "prod-user", productionPassword, "/www"),
            new ServerConfig(draftHost, 21, "draft-user", draftPassword, "/www"),
            @"D:\Release\project",
            string.Empty,
            new BackupConfig(@"D:\Backup", "{yyyy}{MM}{dd}", LogFieldOptions.All));
    }

    private sealed class TestPasswordProtector : IPasswordProtector
    {
        public string Protect(string plainText) => $"protected:{plainText}";

        public string Unprotect(string protectedText) => protectedText.Replace("protected:", string.Empty, StringComparison.Ordinal);
    }
}
