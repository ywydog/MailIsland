namespace MailIsland.Models;

/// <summary>邮件附件信息（仅元数据，下载时按 PartSpecifier 按需拉取）。</summary>
public sealed class MailAttachmentInfo
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    /// <summary>IMAP BODYSTRUCTURE 中的部件路径，如 "2.1"。</summary>
    public string PartSpecifier { get; set; } = "";
}
