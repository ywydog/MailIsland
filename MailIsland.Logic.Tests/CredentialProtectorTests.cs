using MailIsland.Logic.Security;
using Xunit;

namespace MailIsland.Logic.Tests;

public class CredentialProtectorTests
{
    [Fact]
    public void Null_Protector_RoundTrip()
    {
        var p = new NullCredentialProtector();
        const string secret = "授权码ABC123";
        Assert.Equal(secret, p.Decrypt(p.Encrypt(secret)));
    }

    [Fact]
    public void Null_Protector_PreservesEmpty()
    {
        var p = new NullCredentialProtector();
        Assert.Equal("", p.Decrypt(p.Encrypt("")));
    }
}