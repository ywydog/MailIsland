using MailIsland.Logic.Shared;

namespace MailIsland.Logic.Config;

/// <summary>单条关键词规则。</summary>
public sealed class KeywordRule
{
    public string Keyword { get; set; } = "";
    public KeywordMatchScope Scope { get; set; } = KeywordMatchScope.All;
    public bool Enabled { get; set; } = true;
}

/// <summary>全局配置（持久化结构）。</summary>
public sealed class MailIslandConfigData
{
    public List<MailAccountSettings> Accounts { get; set; } = new();
    public int PollIntervalMinutes { get; set; } = 5;
    public int MailCountLimit { get; set; } = 50;
    public bool NotifyEnabled { get; set; } = true;
    public List<KeywordRule> KeywordRules { get; set; } = new();
    /// <summary>提醒正文层显示内容：发件人还是邮件正文。</summary>
    public OverlayContentKind OverlayContentKind { get; set; } = OverlayContentKind.Sender;
    /// <summary>提醒遮罩（标题）显示内容。</summary>
    public NotificationMaskKind MaskContentKind { get; set; } = NotificationMaskKind.AccountName;
    /// <summary>提醒正文层显示方式。</summary>
    public NotificationDisplayMode DisplayMode { get; set; } = NotificationDisplayMode.Auto;
    /// <summary>静态显示时长（秒）。</summary>
    public int StaticDisplaySeconds { get; set; } = 8;
    /// <summary>滚动速度（字符/秒）。</summary>
    public int ScrollCharsPerSecond { get; set; } = 8;
    /// <summary>是否语音朗读提醒内容（默认关闭，避免泄露邮件内容）。</summary>
    public bool NotificationSpeechEnabled { get; set; }
    /// <summary>是否在正文中加载远程图片（默认关闭，避免跟踪像素）。</summary>
    public bool LoadBodyImages { get; set; }
}

/// <summary>账号设置。</summary>
public sealed class MailAccountSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string ImapServer { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public bool ImapUseSsl { get; set; } = true;
    public string SmtpServer { get; set; } = "";
    public int SmtpPort { get; set; } = 465;
    public bool SmtpUseSsl { get; set; } = true;
    /// <summary>DPAPI 加密后的授权码（Base64）。</summary>
    public string EncryptedPassword { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public bool IsSelected { get; set; }
}