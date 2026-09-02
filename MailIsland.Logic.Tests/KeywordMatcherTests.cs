using MailIsland.Logic.Config;
using MailIsland.Logic.Keyword;
using MailIsland.Logic.Shared;
using Xunit;

namespace MailIsland.Logic.Tests;

public class KeywordMatcherTests
{
    [Fact]
    public void MatchesSubjectCaseInsensitive()
    {
        var rules = new List<KeywordRule> { new() { Keyword = "考试", Scope = KeywordMatchScope.Subject } };
        var msg = new MailCandidate("发件人", "sender@qq.com", "关于期末考试的通知", "正文");
        Assert.True(KeywordMatcher.Matches(msg, rules));
    }

    [Fact]
    public void All_ScopeMatchesWhenBodyHits()
    {
        var rules = new List<KeywordRule> { new() { Keyword = "打卡", Scope = KeywordMatchScope.All } };
        var msg = new MailCandidate("张三", "z@qq.com", "周报", "记得每天打卡签到");
        Assert.True(KeywordMatcher.Matches(msg, rules));
    }

    [Fact]
    public void DisabledRuleIgnored()
    {
        var rules = new List<KeywordRule> { new() { Keyword = "x", Scope = KeywordMatchScope.All, Enabled = false } };
        Assert.False(KeywordMatcher.Matches(new MailCandidate("a", "a@q.com", "主题", "正文"), rules));
    }

    [Fact]
    public void SubjectRuleDoesNotMatchBodyOnly()
    {
        var rules = new List<KeywordRule> { new() { Keyword = "考试", Scope = KeywordMatchScope.Subject } };
        var msg = new MailCandidate("a", "a@q.com", "通知", "今天的考试安排");
        Assert.False(KeywordMatcher.Matches(msg, rules));
    }
}