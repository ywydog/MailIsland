namespace MailIsland.Logic.Security;

/// <summary>凭据保护抽象（DPAPI / 明文两种实现）。</summary>
public interface ICredentialProtector
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
}