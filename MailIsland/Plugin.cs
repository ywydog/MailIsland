using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.Automation;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Services;
using MailIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace MailIsland;

/// <summary>邮箱插件入口。</summary>
public partial class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 初始化运行时目录
        GlobalConstants.PluginConfigFolder = PluginConfigFolder;
        GlobalConstants.PluginFolder = Info.PluginFolderPath;

        // 配置需在注册设置页前加载，用于决定是否注册“消息窗口”设置页
        var configHandler = new MailIslandConfigHandler(NullLogger<MailIslandConfigHandler>.Instance);
        services.AddSingleton(configHandler);

        // 基础服务
        services.AddSingleton<IMailClientService, MailClientService>();
        services.AddSingleton<ISmtpClientService, SmtpClientService>();
        services.AddSingleton<Storage.MailCacheStore>();
        services.AddSingleton<MailNotificationProvider>();
        services.AddSingleton<MessageWindowService>();
        services.AddSingleton<MailPollingService>();
        services.AddHostedService(sp => sp.GetRequiredService<MailPollingService>());

        // 注册提醒提供方（自动登记元数据并注册为托管服务）
        services.AddNotificationProvider<MailNotificationProvider>();

        // AddNotificationProvider 会以实现类型额外注册一个 IHostedService，导致同一提供方被构造两次、
        // 在宿主中重复注册。这里改为复用上方注册的单例实例。
        var duplicatedHostedService = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(MailNotificationProvider));
        if (duplicatedHostedService != null)
        {
            services.Remove(duplicatedHostedService);
        }
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MailNotificationProvider>());

        // 注册自动化触发器
        services.AddTrigger<Services.Automation.Triggers.NewMailTrigger, Controls.TriggerSettingsControls.NewMailTriggerSettingsControl>();

        // 注册自动化行动
        services.AddAction<Services.Automation.Actions.SendMailAction, Controls.Automation.SendMailActionSettingsControl>();
        services.AddAction<Services.Automation.Actions.SetMailNotificationEnabledAction, Controls.Automation.NotificationToggleSettingsControl>();
        services.AddAction<Services.Automation.Actions.SetQuietHoursAction, Controls.Automation.NotificationToggleSettingsControl>();
        services.AddAction<Services.Automation.Actions.SendScreenshotAction, Controls.Automation.SendScreenshotActionSettingsControl>();
        services.AddAction<Services.Automation.Actions.SendCameraAction, Controls.Automation.SendCameraActionSettingsControl>();

        // 行动在“添加行动”菜单中归入分组，不单独平铺
        BuildActionMenuTree();

        // 注册设置页分组（折叠下方设置页为一个“邮箱设置”分组）
        services.AddSettingsPageGroup(Shared.GlobalConstants.SettingsGroupId, FluentIcons.MailRegular, "邮箱设置");

        // 注册设置页（多个独立设置页，[Group] 特性自动归入上述分组）
        services.AddSettingsPage<SettingsPage.Views.AccountTabPage>();
        services.AddSettingsPage<SettingsPage.Views.MailTabPage>();
        services.AddSettingsPage<SettingsPage.Views.SentTabPage>();
        services.AddSettingsPage<SettingsPage.Views.NotifyTabPage>();
        services.AddSettingsPage<SettingsPage.Views.TestTabPage>();

        // “消息窗口”页面与“消息提醒”“发件箱”同级，默认不注册，启用消息窗口后才出现
        if (configHandler.Data.MessageWindow.Enabled)
        {
            services.AddSettingsPage<SettingsPage.Views.MessageWindowSettingsPage>();
        }

        // 应用停止时关闭消息窗口并保存配置
        AppBase.Current.AppStopping += (_, _) =>
        {
            IAppHost.TryGetService<MessageWindowService>()?.Shutdown();
            IAppHost.GetService<MailIslandConfigHandler>()?.Save();
        };
    }

    /// <summary>在“添加行动”菜单中构建“MailIsland 行动”分组。</summary>
    private static void BuildActionMenuTree()
    {
        IActionService.ActionMenuTree.Add(new ActionMenuTreeGroup("MailIsland 行动", FluentIcons.MailRegular,
            new ActionMenuTreeItem(Shared.GlobalConstants.SendMailActionId, "发送邮件", FluentIcons.SendRegular),
            new ActionMenuTreeItem(Shared.GlobalConstants.SetMailNotificationActionId, "开关邮件提醒", FluentIcons.MailAlertRegular),
            new ActionMenuTreeItem(Shared.GlobalConstants.SetQuietHoursActionId, "开关免打扰", FluentIcons.AlertOffRegular),
            new ActionMenuTreeItem(Shared.GlobalConstants.SendScreenshotActionId, "将屏幕截图发送", FluentIcons.ScreenshotRegular),
            new ActionMenuTreeItem(Shared.GlobalConstants.SendCameraActionId, "将摄像头画面发送", FluentIcons.CameraRegular)));
    }
}