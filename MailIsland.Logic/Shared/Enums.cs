namespace MailIsland.Logic.Shared;

/// <summary>关键词匹配范围。</summary>
public enum KeywordMatchScope
{
    All,      // 发件人 + 主题 + 正文
    Sender,   // 仅发件人（姓名或邮箱）
    Subject,  // 仅主题
    Body,     // 仅正文
}

/// <summary>预设类型，用于区分是否为国内官方预设。</summary>
public enum PresetKind
{
    Official,   // 内置官方预设
    Custom,     // 自定义
}

/// <summary>提醒正文层显示的内容。</summary>
public enum OverlayContentKind
{
    Sender,      // 发件人 + 主题（旧配置沿用）
    Body,        // 邮件正文
    Subject,     // 仅主题
    SenderOnly,  // 仅发件人
}

/// <summary>提醒遮罩（标题）显示的内容。</summary>
public enum NotificationMaskKind
{
    AccountName,  // 账号名 + 新邮件
    Subject,      // 邮件主题
    Sender,       // 发件人
}

/// <summary>提醒正文层的显示方式。</summary>
public enum NotificationDisplayMode
{
    Auto,    // 短文本静态、长文本滚动
    Static,  // 始终静态显示（超长省略）
    Scroll,  // 始终滚动显示
}