namespace MailIsland.Models;

/// <summary>邮件消息模型。</summary>
public sealed class MailMessage
{
    public uint Uid { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public bool IsUnread { get; set; }
    public string TextBody { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    public bool HasHtml { get; set; }
    public bool HasAttachment { get; set; }
}