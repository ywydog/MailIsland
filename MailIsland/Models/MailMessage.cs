using System.ComponentModel;
using System.Text.Json.Serialization;

namespace MailIsland.Models;

/// <summary>邮件消息模型。</summary>
public sealed class MailMessage : INotifyPropertyChanged
{
    private bool _isUnread;

    public uint Uid { get; set; }
    public string SenderName { get; set; } = "";
    public string SenderEmail { get; set; } = "";
    /// <summary>收件人展示文本（发件箱列表使用）。</summary>
    public string ToDisplay { get; set; } = "";
    public string Subject { get; set; } = "";
    public DateTimeOffset Date { get; set; }
    public bool IsUnread
    {
        get => _isUnread;
        set
        {
            if (_isUnread == value) return;
            _isUnread = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnread)));
        }
    }
    public string TextBody { get; set; } = "";
    public string HtmlBody { get; set; } = "";
    public bool HasHtml { get; set; }
    public bool HasAttachment { get; set; }
    /// <summary>附件元数据列表。</summary>
    public List<MailAttachmentInfo> Attachments { get; set; } = new();
    /// <summary>正文内嵌图片：Content-ID（已规范化）→ MIME 部件路径。</summary>
    public Dictionary<string, string> InlineParts { get; set; } = new();

    /// <summary>发件人显示文本：优先姓名（去掉多余引号），其次邮箱地址。</summary>
    [JsonIgnore]
    public string SenderDisplay => !string.IsNullOrWhiteSpace(SenderName)
        ? SenderName.Trim().Trim('"').Trim()
        : SenderEmail;

    /// <summary>主题显示文本（空主题给出占位）。</summary>
    [JsonIgnore]
    public string SubjectDisplay => string.IsNullOrWhiteSpace(Subject) ? "(无主题)" : Subject;

    /// <summary>收件人显示文本。</summary>
    [JsonIgnore]
    public string RecipientsDisplay => string.IsNullOrWhiteSpace(ToDisplay) ? "(无收件人)" : ToDisplay;

    /// <summary>本地时间文本，供列表展示。</summary>
    [JsonIgnore]
    public string DateDisplay => Date == default ? "" : Date.ToLocalTime().ToString("MM-dd HH:mm");

    public event PropertyChangedEventHandler? PropertyChanged;
}