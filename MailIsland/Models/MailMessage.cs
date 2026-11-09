using System.Text.Json.Serialization;

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
    /// <summary>附件元数据列表。</summary>
    public List<MailAttachmentInfo> Attachments { get; set; } = new();
    /// <summary>正文内嵌图片：Content-ID（已规范化）→ MIME 部件路径。</summary>
    public Dictionary<string, string> InlineParts { get; set; } = new();

    /// <summary>发件人显示文本：优先姓名，其次邮箱地址。</summary>
    [JsonIgnore]
    public string SenderDisplay => !string.IsNullOrWhiteSpace(SenderName)
        ? SenderName
        : SenderEmail;

    /// <summary>主题显示文本（空主题给出占位）。</summary>
    [JsonIgnore]
    public string SubjectDisplay => string.IsNullOrWhiteSpace(Subject) ? "(无主题)" : Subject;

    /// <summary>本地时间文本，供列表展示。</summary>
    [JsonIgnore]
    public string DateDisplay => Date == default ? "" : Date.ToLocalTime().ToString("MM-dd HH:mm");
}