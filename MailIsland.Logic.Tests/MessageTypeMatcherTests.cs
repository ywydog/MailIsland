using MailIsland.Logic.Config;
using MailIsland.Logic.Messages;
using Xunit;

namespace MailIsland.Logic.Tests;

public class MessageTypeMatcherTests
{
    [Fact]
    public void MatchesSubjectKeyword()
    {
        var rule = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" };

        var matched = MessageTypeMatcher.Match("【作业通知】第三章练习", new[] { rule });

        Assert.Same(rule, matched);
    }

    [Fact]
    public void MatchesCaseInsensitive()
    {
        var rule = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "homework" };

        var matched = MessageTypeMatcher.Match("HOMEWORK update", new[] { rule });

        Assert.Same(rule, matched);
    }

    [Fact]
    public void FirstEnabledMatchWins()
    {
        var disabled = new MessageTypeRule
        {
            Name = "旧类型",
            SubjectKeyword = "作业",
            Enabled = false,
        };
        var enabled = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" };

        var matched = MessageTypeMatcher.Match("作业提醒", new[] { disabled, enabled });

        Assert.Same(enabled, matched);
    }

    [Fact]
    public void SkipsRulesWithoutNameOrKeyword()
    {
        var noName = new MessageTypeRule { Name = "", SubjectKeyword = "作业" };
        var noKeyword = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "" };
        var valid = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" };

        var matched = MessageTypeMatcher.Match("作业提醒", new[] { noName, noKeyword, valid });

        Assert.Same(valid, matched);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptySubjectReturnsNull(string subject)
    {
        var rule = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" };

        Assert.Null(MessageTypeMatcher.Match(subject, new[] { rule }));
    }

    [Fact]
    public void NoMatchReturnsNull()
    {
        var rule = new MessageTypeRule { Name = "作业通知", SubjectKeyword = "作业" };

        Assert.Null(MessageTypeMatcher.Match("期末安排", new[] { rule }));
    }
}
