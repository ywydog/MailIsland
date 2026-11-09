using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace MailIsland.Logic.Security;

/// <summary>
/// 使用 Windows DPAPI (CurrentUser) 加密，输出 Base64。仅 Windows 可用。
/// <para><paramref name="entropy"/> 作为盐参与加解密：同一份密文在不同盐下无法解开，
/// 可把数据绑定到当前用户与当前插件实例。</para>
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsCredentialProtector(byte[]? entropy = null) : ICredentialProtector
{
    private readonly byte[]? _entropy = entropy;

    public string Encrypt(string plainText)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var enc = ProtectedData.Protect(bytes, _entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    public string Decrypt(string cipherText)
    {
        var enc = Convert.FromBase64String(cipherText);
        var plain = ProtectedData.Unprotect(enc, _entropy, DataProtectionScope.CurrentUser);
        return System.Text.Encoding.UTF8.GetString(plain);
    }
}
