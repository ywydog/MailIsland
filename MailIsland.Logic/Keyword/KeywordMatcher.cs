using MailIsland.Logic.Config;
using MailIsland.Logic.Shared;

namespace MailIsland.Logic.Keyword;

/// <summary>用于关键词匹配的邮件候选数据。</summary>
public readonly record struct MailCandidate(string SenderName, string SenderEmail, string Subject, string Body);

/// <summary>关键词匹配器。</summary>
public static class KeywordMatcher
{
    public static bool Matches(MailCandidate candidate, IEnumerable<KeywordRule> rules)
    {
        foreach (var rule in rules)
        {
            if (!rule.Enabled || string.IsNullOrWhiteSpace(rule.Keyword))
                continue;

            if (ScopeMatches(rule.Scope, candidate, rule.Keyword))
                return true;
        }
        return false;
    }

    private static bool ScopeMatches(KeywordMatchScope scope, MailCandidate c, string kw)
    {
        var sender = $"{c.SenderName} {c.SenderEmail}";
        return scope switch
        {
            KeywordMatchScope.Sender => Contains(sender, kw),
            KeywordMatchScope.Subject => Contains(c.Subject, kw),
            KeywordMatchScope.Body => Contains(c.Body, kw),
            _ => Contains(sender, kw) || Contains(c.Subject, kw) || Contains(c.Body, kw),
        };
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}