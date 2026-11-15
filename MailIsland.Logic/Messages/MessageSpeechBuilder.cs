using MailIsland.Logic.Config;

namespace MailIsland.Logic.Messages;

/// <summary>按消息类型生成语音播报文本。</summary>
public static class MessageSpeechBuilder
{
    /// <summary>
    /// 优先使用命中类型的播报文本，否则回退到全局模板。
    /// 支持 {sender}、{type}、{subject} 占位符；结果为空表示不需要播报。
    /// </summary>
    public static string Build(
        string sender,
        string subject,
        MessageTypeRule? type,
        string? fallbackTemplate)
    {
        var template = !string.IsNullOrWhiteSpace(type?.SpeechText)
            ? type.SpeechText
            : fallbackTemplate;

        if (string.IsNullOrWhiteSpace(template))
        {
            return "";
        }

        return template
            .Replace("{sender}", sender ?? "", StringComparison.Ordinal)
            .Replace("{type}", type?.Name ?? "", StringComparison.Ordinal)
            .Replace("{subject}", subject ?? "", StringComparison.Ordinal)
            .Trim();
    }
}
