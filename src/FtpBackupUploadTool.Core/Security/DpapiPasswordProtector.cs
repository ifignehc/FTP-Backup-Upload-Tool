using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace FtpBackupUploadTool.Core.Security;

public sealed class DpapiPasswordProtector : IPasswordProtector
{
    public string Protect(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectBytes(plainBytes);

        return Convert.ToBase64String(protectedBytes);
    }

    public string Unprotect(string protectedText)
    {
        var protectedBytes = Convert.FromBase64String(protectedText);
        var plainBytes = UnprotectBytes(protectedBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] ProtectBytes(byte[] plainBytes)
    {
        return Transform(plainBytes, CryptProtectData);
    }

    private static byte[] UnprotectBytes(byte[] protectedBytes)
    {
        return Transform(protectedBytes, CryptUnprotectData);
    }

    private static byte[] Transform(byte[] input, DpapiTransform transform)
    {
        var inputBlob = CreateBlob(input);
        var outputBlob = new DataBlob();

        try
        {
            if (!transform(ref inputBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 0, ref outputBlob))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var output = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, output, 0, output.Length);

            return output;
        }
        finally
        {
            if (inputBlob.Data != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(inputBlob.Data);
            }

            if (outputBlob.Data != IntPtr.Zero)
            {
                _ = LocalFree(outputBlob.Data);
            }
        }
    }

    private static DataBlob CreateBlob(byte[] data)
    {
        var blob = new DataBlob
        {
            Length = data.Length,
            Data = Marshal.AllocHGlobal(data.Length)
        };

        Marshal.Copy(data, 0, blob.Data, data.Length);

        return blob;
    }

    private delegate bool DpapiTransform(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(
        ref DataBlob dataIn,
        string? dataDescription,
        IntPtr optionalEntropy,
        IntPtr reserved,
        IntPtr promptStruct,
        int flags,
        ref DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int Length;
        public IntPtr Data;
    }
}
