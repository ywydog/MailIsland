using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services.Automation.Actions;

/// <summary>“开关邮件提醒”自动化行动。</summary>
[ActionInfo(GlobalConstants.SetMailNotificationActionId, "开关邮件提醒", "\uE7ED", addDefaultToMenu: false)]
public class SetMailNotificationEnabledAction(
    ILogger<SetMailNotificationEnabledAction> logger,
    MailIslandConfigHandler config) : ActionBase<NotificationToggleSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var enabled = Resolve(Settings.Mode, config.Data.NotifyEnabled);
        config.Data.NotifyEnabled = enabled;
        config.Save();

        logger.LogInformation("邮件提醒已{State}", enabled ? "开启" : "关闭");
    }

    private static bool Resolve(NotificationToggleMode mode, bool current) => mode switch
    {
        NotificationToggleMode.Enable => true,
        NotificationToggleMode.Disable => false,
        NotificationToggleMode.Toggle => !current,
        _ => current,
    };
}
