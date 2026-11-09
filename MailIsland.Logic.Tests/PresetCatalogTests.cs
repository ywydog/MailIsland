using MailIsland.Logic.Presets;
using MailIsland.Logic.Shared;
using Xunit;

namespace MailIsland.Logic.Tests;

public class PresetCatalogTests
{
    [Fact]
    public void ContainsQqImapServer()
    {
        var p = MailPresetCatalog.FindByDisplayName("QQ邮箱");
        Assert.NotNull(p);
        Assert.Equal("imap.qq.com", p!.ImapServer);
        Assert.Equal(993, p.ImapPort);
    }

    [Fact]
    public void HasCustomEntry()
    {
        Assert.Contains(MailPresetCatalog.Presets, p => p.Kind == PresetKind.Custom);
    }

    [Theory]
    [InlineData("139邮箱", "imap.139.com")]
    [InlineData("189邮箱", "imap.189.cn")]
    [InlineData("网易企业邮箱", "imap.qiye.163.com")]
    [InlineData("yeah.net 邮箱", "imap.yeah.net")]
    [InlineData("188邮箱", "imap.188.com")]
    [InlineData("Foxmail 邮箱", "imap.qq.com")]
    public void HasExpectedImapServer(string displayName, string imapServer)
    {
        var preset = MailPresetCatalog.FindByDisplayName(displayName);

        Assert.NotNull(preset);
        Assert.Equal(imapServer, preset!.ImapServer);
        Assert.Equal(993, preset.ImapPort);
        Assert.True(preset.ImapSsl);
    }

    [Fact]
    public void OfficialPresetsAreComplete()
    {
        foreach (var preset in MailPresetCatalog.Presets.Where(p => p.Kind == PresetKind.Official))
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.ImapServer));
            Assert.False(string.IsNullOrWhiteSpace(preset.SmtpServer));
            Assert.InRange(preset.ImapPort, 1, 65535);
            Assert.InRange(preset.SmtpPort, 1, 65535);
        }
    }

    [Fact]
    public void DisplayNamesAreUnique()
    {
        var names = MailPresetCatalog.Presets.Select(p => p.DisplayName).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }
}