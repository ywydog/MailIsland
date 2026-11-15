using MailIsland.Logic.Config;

namespace MailIsland.Models;

/// <summary>一次消息窗口显示所需的邮件、账号与设置快照。</summary>
public sealed class MessageWindowRequest
{
    public required MailMessage Mail { get; init; }

    /// <summary>预览模式下没有真实账号。</summary>
    public MailAccountSettings? Account { get; init; }

    public required MessageWindowSettings Settings { get; init; }

    /// <summary>主题命中的消息类型；未命中时为 null。</summary>
    public MessageTypeRule? TypeRule { get; init; }

    public bool IsPreview { get; init; }
}
