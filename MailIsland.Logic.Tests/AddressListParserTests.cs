using MailIsland.Logic.Mail;
using Xunit;

namespace MailIsland.Logic.Tests;

public class AddressListParserTests
{
    [Fact]
    public void ParsesMultipleSeparators()
    {
        var result = AddressListParser.Parse("a@x.com; b@y.com, c@z.com，d@w.com");

        Assert.Equal(4, result.Addresses.Count);
        Assert.False(result.HasInvalid);
    }

    [Fact]
    public void ParsesDisplayName()
    {
        var result = AddressListParser.Parse("\"张三\" <zhangsan@example.com>");

        var entry = Assert.Single(result.Addresses);
        Assert.Equal("张三", entry.Name);
        Assert.Equal("zhangsan@example.com", entry.Address);
    }

    [Fact]
    public void CollectsInvalidEntries()
    {
        var result = AddressListParser.Parse("bad-address, ok@example.com");

        Assert.Single(result.Addresses);
        Assert.Single(result.Invalid);
        Assert.Equal("bad-address", result.Invalid[0]);
    }

    [Fact]
    public void EmptyInputReturnsEmpty()
    {
        Assert.Empty(AddressListParser.Parse(null).Addresses);
        Assert.Empty(AddressListParser.Parse("   ").Addresses);
    }
}
