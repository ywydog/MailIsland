using System.Text.Json.Serialization;

namespace MailIsland.Models;

/// <summary>邮件附件信息（仅元数据，下载时按 PartSpecifier 按需拉取）。</summary>
public sealed class MailAttachmentInfo
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    /// <summary>IMAP BODYSTRUCTURE 中的部件路径，如 "2.1"。</summary>
    public string PartSpecifier { get; set; } = "";

    /// <summary>人类可读的大小文本，仅用于界面显示。</summary>
    [JsonIgnore]
    public string SizeDisplay
    {
        get
        {
            if (Size <= 0) return "";
            if (Size < 1024) return $"{Size} B";
            if (Size < 1024 * 1024) return $"{Size / 1024.0:0.#} KB";
            return $"{Size / (1024.0 * 1024):0.#} MB";
        }
    }
}
