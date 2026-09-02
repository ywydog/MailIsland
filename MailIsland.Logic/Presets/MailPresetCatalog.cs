using MailIsland.Logic.Shared;

namespace MailIsland.Logic.Presets;

/// <summary>国内邮箱预设（全部授权码、非 OAuth）。</summary>
public sealed record MailPreset(
    string DisplayName,
    string ImapServer,
    int ImapPort,
    bool ImapSsl,
    string SmtpServer,
    int SmtpPort,
    bool SmtpSsl,
    PresetKind Kind);

/// <summary>内置预设目录。</summary>
public static class MailPresetCatalog
{
    public static IReadOnlyList<MailPreset> Presets { get; } = new List<MailPreset>
    {
        new("QQ邮箱",    "imap.qq.com", 993, true, "smtp.qq.com", 465, true, PresetKind.Official),
        new("163邮箱",   "imap.163.com", 993, true, "smtp.163.com", 465, true, PresetKind.Official),
        new("126邮箱",   "imap.126.com", 993, true, "smtp.126.com", 465, true, PresetKind.Official),
        new("新浪邮箱",  "imap.sina.com", 993, true, "smtp.sina.com", 465, true, PresetKind.Official),
        new("搜狐邮箱",  "imap.sohu.com", 993, true, "smtp.sohu.com", 465, true, PresetKind.Official),
        new("阿里企业邮箱", "imap.qiye.aliyun.com", 993, true, "smtp.qiye.aliyun.com", 465, true, PresetKind.Official),
        new("腾讯企业邮箱", "imap.exmail.qq.com", 993, true, "smtp.exmail.qq.com", 465, true, PresetKind.Official),
        new("网易企业邮箱", "imap.ym.163.com", 993, true, "smtp.ym.163.com", 465, true, PresetKind.Official),
        new("自定义", "", 993, true, "", 465, true, PresetKind.Custom),
    };

    public static MailPreset? FindByDisplayName(string displayName) =>
        Presets.FirstOrDefault(p => p.DisplayName == displayName);
}