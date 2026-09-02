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
}