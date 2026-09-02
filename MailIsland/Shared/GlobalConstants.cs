namespace MailIsland.Shared;

/// <summary>插件级常量与运行时目录。</summary>
public static class GlobalConstants
{
    public static string PluginConfigFolder { get; set; } = "";
    public static string PluginFolder { get; set; } = "";

    /// <summary>提醒提供方 GUID（唯一标识，勿与他人重复）。</summary>
    public const string NotificationProviderGuid = "A1B2C3D4-5E6F-7890-ABCD-EF1234567890";
}