namespace MailIsland.Models.Automation;

/// <summary>开关型调整方式。</summary>
public enum NotificationToggleMode
{
    NoChange,  // 不改变
    Enable,    // 开启
    Disable,   // 关闭
    Toggle,    // 切换
}

/// <summary>“调整邮件提醒”自动化行动设置。</summary>
public class AdjustMailNotificationActionSettings
{
    /// <summary>对“启用提醒”的调整方式。</summary>
    public NotificationToggleMode NotifyMode { get; set; } = NotificationToggleMode.Disable;

    /// <summary>对“免打扰时段”的调整方式。</summary>
    public NotificationToggleMode QuietHoursMode { get; set; } = NotificationToggleMode.NoChange;
}
