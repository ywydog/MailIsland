namespace MailIsland.Logic.Config;

/// <summary>消息窗口增强配置。</summary>
public sealed class MessageWindowSettings
{
    public const int MinDisplaySeconds = 3;
    public const int MaxDisplaySeconds = 120;
    public const int MinWindowWidth = 360;
    public const int MaxWindowWidth = 1200;
    public const int MinWindowHeight = 380;
    public const int MaxWindowHeight = 900;
    public const int DefaultWindowWidth = 520;
    public const int DefaultWindowHeight = 400;
    public const int DefaultDisplaySeconds = 15;
    public const string DefaultAcknowledgeText = "收到";

    /// <summary>是否启用消息窗口。关闭时不注册“提醒增强”分组与页面。</summary>
    public bool Enabled { get; set; }

    /// <summary>正文无需滚动时的停留秒数；长正文在滚动结束后开始倒计时。</summary>
    public int DisplaySeconds { get; set; } = DefaultDisplaySeconds;

    /// <summary>正文超出可视区域时是否自动向下滚动。</summary>
    public bool AutoScroll { get; set; } = true;

    /// <summary>默认窗口宽度（DIP）。</summary>
    public int WindowWidth { get; set; } = DefaultWindowWidth;

    /// <summary>默认窗口高度（DIP）。</summary>
    public int WindowHeight { get; set; } = DefaultWindowHeight;

    /// <summary>确认按钮文字。</summary>
    public string AcknowledgeText { get; set; } = DefaultAcknowledgeText;

    /// <summary>点击确认按钮时是否把该邮件全部附件下载到桌面。</summary>
    public bool DownloadAttachmentsOnAcknowledge { get; set; }

    /// <summary>是否朗读消息类型相关的播报文本。</summary>
    public bool SpeechEnabled { get; set; }

    /// <summary>未命中类型规则或类型未配置播报文本时使用的模板。</summary>
    public string DefaultSpeechText { get; set; } = "{sender} 发来新邮件";

    /// <summary>主题消息类型规则，按顺序取第一条命中项。</summary>
    public List<MessageTypeRule> TypeRules { get; set; } = new();

    /// <summary>把配置收敛到允许范围，避免手改 JSON 造成布局或计时异常。</summary>
    public void Normalize()
    {
        DisplaySeconds = Math.Clamp(DisplaySeconds, MinDisplaySeconds, MaxDisplaySeconds);
        WindowWidth = Math.Clamp(WindowWidth, MinWindowWidth, MaxWindowWidth);
        WindowHeight = Math.Clamp(WindowHeight, MinWindowHeight, MaxWindowHeight);
        AcknowledgeText = string.IsNullOrWhiteSpace(AcknowledgeText)
            ? DefaultAcknowledgeText
            : AcknowledgeText.Trim();
        TypeRules ??= new List<MessageTypeRule>();

        foreach (var rule in TypeRules)
        {
            rule.Name ??= "";
            rule.SubjectKeyword ??= "";
            rule.SpeechText ??= "";
        }
    }

    /// <summary>深拷贝配置，供窗口在显示期间使用稳定的设置快照。</summary>
    public MessageWindowSettings Clone() => new()
    {
        Enabled = Enabled,
        DisplaySeconds = DisplaySeconds,
        AutoScroll = AutoScroll,
        WindowWidth = WindowWidth,
        WindowHeight = WindowHeight,
        AcknowledgeText = AcknowledgeText,
        DownloadAttachmentsOnAcknowledge = DownloadAttachmentsOnAcknowledge,
        SpeechEnabled = SpeechEnabled,
        DefaultSpeechText = DefaultSpeechText,
        TypeRules = (TypeRules ?? new List<MessageTypeRule>()).Select(rule => rule.Clone()).ToList(),
    };
}

/// <summary>主题关键词到消息类型的映射规则。</summary>
public sealed class MessageTypeRule
{
    /// <summary>显示在窗口中的消息类型名，如“作业通知”。</summary>
    public string Name { get; set; } = "";

    /// <summary>主题包含该关键词时命中，忽略大小写。</summary>
    public string SubjectKeyword { get; set; } = "";

    /// <summary>该类型的播报文本，可空；支持 {sender}、{type}、{subject} 占位符。</summary>
    public string SpeechText { get; set; } = "";

    public bool Enabled { get; set; } = true;

    public MessageTypeRule Clone() => new()
    {
        Name = Name,
        SubjectKeyword = SubjectKeyword,
        SpeechText = SpeechText,
        Enabled = Enabled,
    };
}
