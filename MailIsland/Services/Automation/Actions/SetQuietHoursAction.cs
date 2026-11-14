using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using MailIsland.ConfigHandlers;
using MailIsland.Models.Automation;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services.Automation.Actions;

/// <summary>“开关免打扰”自动化行动。</summary>
[ActionInfo(GlobalConstants.SetQuietHoursActionId, "开关免打扰", "\uE708", addDefaultToMenu: false)]
public class SetQuietHoursAction(
    ILogger<SetQuietHoursAction> logger,
    MailIslandConfigHandler config) : ActionBase<NotificationToggleSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var enabled = Resolve(Settings.Mode, config.Data.QuietHoursEnabled);
        config.Data.QuietHoursEnabled = enabled;
        config.Save();

        logger.LogInformation("免打扰已{State}", enabled ? "开启" : "关闭");
    }

    private static bool Resolve(NotificationToggleMode mode, bool current) => mode switch
    {
        NotificationToggleMode.Enable => true,
        NotificationToggleMode.Disable => false,
        NotificationToggleMode.Toggle => !current,
        _ => current,
    };
}
