using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace MailIsland.Logic.Security;

/// <summary>使用 Windows DPAPI (CurrentUser) 加密，输出 Base64。仅 Windows 可用。</summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialProtector : ICredentialProtector
{
    public string Encrypt(string plainText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var enc = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    public string Decrypt(string cipherText)
    {
        var enc = Convert.FromBase64String(cipherText);
        var plain = ProtectedData.Unprotect(enc, null, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(plain);
    }
}