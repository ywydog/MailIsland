using System.Text.Json.Serialization;

namespace MailIsland.Models;

/// <summary>写邮件时待发送的附件。</summary>
public sealed class ComposeAttachment
{
    public string FilePath { get; init; } = "";
    public long Size { get; init; }

    [JsonIgnore]
    public string FileName => Path.GetFileName(FilePath);

    [JsonIgnore]
    public string SizeDisplay => ByteSize.Format(Size);
}
