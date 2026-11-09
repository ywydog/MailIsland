using MailIsland.Logic.Shared;

namespace MailIsland.Logic.Presets;

/// <summary>国内邮箱预设（全部使用授权码/客户端专用密码，非 OAuth）。</summary>
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
        new("QQ邮箱",      "imap.qq.com", 993, true, "smtp.qq.com", 465, true, PresetKind.Official),
        new("Foxmail 邮箱", "imap.qq.com", 993, true, "smtp.qq.com", 465, true, PresetKind.Official),
        new("163邮箱",     "imap.163.com", 993, true, "smtp.163.com", 465, true, PresetKind.Official),
        new("126邮箱",     "imap.126.com", 993, true, "smtp.126.com", 465, true, PresetKind.Official),
        new("yeah.net 邮箱", "imap.yeah.net", 993, true, "smtp.yeah.net", 465, true, PresetKind.Official),
        new("188邮箱",     "imap.188.com", 993, true, "smtp.188.com", 465, true, PresetKind.Official),
        new("新浪邮箱",    "imap.sina.com", 993, true, "smtp.sina.com", 465, true, PresetKind.Official),
        new("搜狐邮箱",    "imap.sohu.com", 993, true, "smtp.sohu.com", 465, true, PresetKind.Official),
        new("139邮箱",     "imap.139.com", 993, true, "smtp.139.com", 465, true, PresetKind.Official),
        new("189邮箱",     "imap.189.cn", 993, true, "smtp.189.cn", 465, true, PresetKind.Official),
        new("阿里企业邮箱", "imap.qiye.aliyun.com", 993, true, "smtp.qiye.aliyun.com", 465, true, PresetKind.Official),
        new("腾讯企业邮箱", "imap.exmail.qq.com", 993, true, "smtp.exmail.qq.com", 465, true, PresetKind.Official),
        // 网易企业邮箱官方地址；华东区域也可用 imaphz/smtphz.qiye.163.com
        new("网易企业邮箱", "imap.qiye.163.com", 993, true, "smtp.qiye.163.com", 465, true, PresetKind.Official),
        // 网易域名邮箱（旧版企业邮）
        new("网易域名邮箱", "imap.ym.163.com", 993, true, "smtp.ym.163.com", 465, true, PresetKind.Official),
        new("自定义", "", 993, true, "", 465, true, PresetKind.Custom),
    };

    public static MailPreset? FindByDisplayName(string displayName) =>
        Presets.FirstOrDefault(p => p.DisplayName == displayName);
}