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

    public static void SavedPasswordsEnableRememberPasswordAndCanBeDisplayed()
    {
        var existing = CreateProcess(
            "常规更新",
            "prod-a",
            "draft-a",
            "protected:prod-secret",
            "protected:draft-secret");
        var viewModel = new SettingsViewModel();

        viewModel.LoadProcesses(new[] { existing }, "常规更新");

        TestAssert.True(viewModel.RememberProductionPassword, "saved production password should enable remember password");
        TestAssert.True(viewModel.RememberDraftPassword, "saved draft password should enable remember password");
        TestAssert.Equal(
            "prod-secret",
            viewModel.GetProductionPasswordForDisplay(new TestPasswordProtector()),
            "remembered production password should be available for display");
        TestAssert.Equal(
            "draft-secret",
            viewModel.GetDraftPasswordForDisplay(new TestPasswordProtector()),
            "remembered draft password should be available for display");
    }

    public static void TurningOffRememberPasswordClearsSavedPasswords()
    {
        var existing = CreateProcess("常规更新", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { existing }, "常规更新");
        viewModel.RememberProductionPassword = false;
        viewModel.RememberDraftPassword = false;

        var saved = viewModel.BuildProcessConfig(new TestPasswordProtector(), string.Empty, string.Empty, existing);

        TestAssert.Equal(string.Empty, saved.ProductionServer.EncryptedPassword, "production password should be cleared when remember password is off");
        TestAssert.Equal(string.Empty, saved.DraftServer.EncryptedPassword, "draft password should be cleared when remember password is off");
    }

    public static void AddProcessCreatesBlankDraftWithUniqueName()
    {
        var existing = CreateProcess("test", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { existing }, "test");

        viewModel.AddProcess();

        TestAssert.Equal(2, viewModel.Processes.Count, "new process should be added to process list");
        TestAssert.Equal(viewModel.SelectedProcess, viewModel.ProcessName, "new draft name should match selected process");
        TestAssert.True(!string.Equals("test", viewModel.SelectedProcess, StringComparison.OrdinalIgnoreCase), "new draft should use a unique name");
        TestAssert.Equal("192.168.1.10", viewModel.ProductionHost, "new draft should reset production host to defaults");
        TestAssert.Equal("192.168.1.20", viewModel.DraftHost, "new draft should reset draft host to defaults");
        TestAssert.True(!viewModel.RememberProductionPassword, "new draft should not inherit production password state");
        TestAssert.True(!viewModel.RememberDraftPassword, "new draft should not inherit draft password state");
    }

    public static void CopySelectedProcessCreatesDraftWithCurrentFields()
    {
        var existing = CreateProcess("test", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { existing }, "test");

        viewModel.CopySelectedProcess();

        TestAssert.Equal(2, viewModel.Processes.Count, "copied process should be added to process list");
        TestAssert.Equal(viewModel.SelectedProcess, viewModel.ProcessName, "copied draft name should match selected process");
        TestAssert.True(viewModel.SelectedProcess.StartsWith("test", StringComparison.OrdinalIgnoreCase), "copied draft should be based on selected name");
        TestAssert.Equal("prod-a", viewModel.ProductionHost, "copied draft should keep production host");
        TestAssert.Equal("draft-a", viewModel.DraftHost, "copied draft should keep draft host");
    }

    public static void DeleteSelectedProcessRemovesItAndSelectsRemainingProcess()
    {
        var first = CreateProcess("first", "prod-a", "draft-a", "enc-prod-a", "enc-draft-a");
        var second = CreateProcess("second", "prod-b", "draft-b", "enc-prod-b", "enc-draft-b");
        var viewModel = new SettingsViewModel();
        viewModel.LoadProcesses(new[] { first, second }, "first");

        var deleted = viewModel.DeleteSelectedProcess();

        TestAssert.True(deleted, "delete should report success when a process is selected");
        TestAssert.Equal(1, viewModel.Processes.Count, "selected process should be removed");
        TestAssert.Equal("second", viewModel.SelectedProcess, "remaining process should be selected");
        TestAssert.Equal("prod-b", viewModel.ProductionHost, "remaining process fields should load");
    }

    public static void BuildsAndLoadsActiveFtpModeSettings()
    {
        var viewModel = new SettingsViewModel
        {
            ProductionUsePassive = false,
            DraftUsePassive = false
        };

        var saved = viewModel.BuildProcessConfig(new TestPasswordProtector(), "prod-secret", "draft-secret");

        TestAssert.True(!saved.ProductionServer.UsePassive, "production server should save active FTP mode");
        TestAssert.True(!saved.DraftServer.UsePassive, "draft server should save active FTP mode");

        viewModel.ProductionUsePassive = true;
        viewModel.DraftUsePassive = true;
        viewModel.LoadProcess(saved);

        TestAssert.True(!viewModel.ProductionUsePassive, "production active FTP mode should load into settings");
        TestAssert.True(!viewModel.DraftUsePassive, "draft active FTP mode should load into settings");
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
