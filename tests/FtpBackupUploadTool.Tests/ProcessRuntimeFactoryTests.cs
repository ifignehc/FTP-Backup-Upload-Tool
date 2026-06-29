using FtpBackupUploadTool.App.Runtime;
using FtpBackupUploadTool.Core.Models;
using FtpBackupUploadTool.Core.Security;

namespace FtpBackupUploadTool.Tests;

internal static class ProcessRuntimeFactoryTests
{
    public static void EmptySavedPasswordsCreateRuntimeWithBlankPasswords()
    {
        var process = new ProcessConfig(
            "new-process",
            new ServerConfig("127.0.0.1", 21, "prod-user", string.Empty, "/"),
            new ServerConfig("127.0.0.1", 21, "draft-user", string.Empty, "/"),
            @"D:\Release\project",
            string.Empty,
            new BackupConfig(@"D:\Backup", "{yyyy}{MM}{dd}", LogFieldOptions.All));
        var factory = new ProcessRuntimeFactory(new ThrowingPasswordProtector());

        var services = factory.Create(process);

        TestAssert.True(services.ProductionClient is not null, "production client should be created when password is blank");
        TestAssert.True(services.DraftClient is not null, "draft client should be created when password is blank");
    }

    private sealed class ThrowingPasswordProtector : IPasswordProtector
    {
        public string Protect(string plainText) => plainText;

        public string Unprotect(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText))
            {
                throw new InvalidOperationException("empty passwords should not be decrypted");
            }

            return protectedText;
        }
    }
}
