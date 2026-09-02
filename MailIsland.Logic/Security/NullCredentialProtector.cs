namespace MailIsland.Logic.Security;

/// <summary>明文往返实现（用于非 Windows 测试环境或不可用 DPAPI 时）。</summary>
public sealed class NullCredentialProtector : ICredentialProtector
{
    public string Encrypt(string plainText) => plainText;
    public string Decrypt(string cipherText) => cipherText;
}