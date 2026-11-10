using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services.Automation.Actions;

/// <summary>“调整邮件提醒”自动化行动：开关提醒或免打扰。</summary>
[ActionInfo(GlobalConstants.AdjustMailNotificationActionId, "调整邮件提醒", "\uE7ED")]
public class AdjustMailNotificationAction(
    ILogger<AdjustMailNotificationAction> logger,
    MailIslandConfigHandler config) : ActionBase<AdjustMailNotificationActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var notifyEnabled = Resolve(Settings.NotifyMode, config.Data.NotifyEnabled);
        var quietHoursEnabled = Resolve(Settings.QuietHoursMode, config.Data.QuietHoursEnabled);

        config.Data.NotifyEnabled = notifyEnabled;
        config.Data.QuietHoursEnabled = quietHoursEnabled;
        config.Save();

        logger.LogInformation(
            "邮件提醒已调整：启用提醒={NotifyEnabled}，免打扰={QuietHoursEnabled}",
            notifyEnabled, quietHoursEnabled);
    }

    private static bool Resolve(NotificationToggleMode mode, bool current) => mode switch
    {
        NotificationToggleMode.Enable => true,
        NotificationToggleMode.Disable => false,
        NotificationToggleMode.Toggle => !current,
        _ => current,
    };
}
