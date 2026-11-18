using MailIsland.Logic.Config;

namespace MailIsland.Logic.Messages;

/// <summary>根据邮件主题匹配用户定义的消息类型。</summary>
public static class MessageTypeMatcher
{
    /// <summary>按顺序返回第一条启用且命中的规则；无命中返回 null。</summary>
    public static MessageTypeRule? Match(string? subject, IEnumerable<MessageTypeRule>? rules)
    {
        if (string.IsNullOrWhiteSpace(subject) || rules is null)
        {
            return null;
        }

        foreach (var rule in rules)
        {
            if (!rule.Enabled ||
                string.IsNullOrWhiteSpace(rule.Name) ||
                string.IsNullOrWhiteSpace(rule.SubjectKeyword))
            {
                continue;
            }

            if (subject.Contains(rule.SubjectKeyword, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        return null;
    }
}
