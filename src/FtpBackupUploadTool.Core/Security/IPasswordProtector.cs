namespace FtpBackupUploadTool.Core.Security;

public interface IPasswordProtector
{
    string Protect(string plainText);

    string Unprotect(string protectedText);
}
