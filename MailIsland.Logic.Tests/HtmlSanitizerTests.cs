using MailIsland.Logic.Html;
using Xunit;

namespace MailIsland.Logic.Tests;

public class HtmlSanitizerTests
{
    [Fact]
    public void RemovesScriptTagsAndContent()
    {
        var html = "<p>你好</p><script>alert(1)</script>";
        var clean = HtmlSanitizer.Sanitize(html);
        Assert.DoesNotContain("alert", clean);
        Assert.Contains("你好", clean);
    }

    [Fact]
    public void RemovesEventAttributes()
    {
        var html = "<a href=\"https://x.com\" onclick=\"evil()\">链接</a>";
        var clean = HtmlSanitizer.Sanitize(html);
        Assert.DoesNotContain("onclick", clean, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://x.com", clean);
    }

    [Fact]
    public void RemovesJavascriptUri()
    {
        var html = "<a href=\"javascript:alert(1)\">x</a>";
        var clean = HtmlSanitizer.Sanitize(html);
        Assert.DoesNotContain("javascript:", clean, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreservesPlainText()
    {
        Assert.Equal("hello", HtmlSanitizer.Sanitize("hello"));
    }

    [Fact]
    public void NullOrWhitespace()
    {
        Assert.Equal("", HtmlSanitizer.Sanitize(null));
        Assert.Equal("", HtmlSanitizer.Sanitize("   "));
    }
}