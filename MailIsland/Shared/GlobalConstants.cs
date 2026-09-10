namespace MailIsland.Shared;

/// <summary>插件级常量与运行时目录。</summary>
public static class GlobalConstants
{
    public static string PluginConfigFolder { get; set; } = "";
    public static string PluginFolder { get; set; } = "";

    /// <summary>提醒提供方 GUID（唯一标识，勿与他人重复）。</summary>
    public const string NotificationProviderGuid = "2f7d9c1e-8b4a-4f5d-9e3c-1a2b6d7e8c90";

    /// <summary>“新邮件命中关键词”自动化触发器 ID（唯一，勿与他人重复）。</summary>
    public const string NewMailTriggerId = "mailisland.newmail.keyword";
}