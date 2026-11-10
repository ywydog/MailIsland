namespace MailIsland.Models.Automation;

/// <summary>开关型行动的调整方式。</summary>
public enum NotificationToggleMode
{
    Enable,   // 开启
    Disable,  // 关闭
    Toggle,   // 切换
}

/// <summary>“开关邮件提醒 / 开关免打扰”行动设置。</summary>
public class NotificationToggleSettings
{
    public NotificationToggleMode Mode { get; set; } = NotificationToggleMode.Disable;
}
