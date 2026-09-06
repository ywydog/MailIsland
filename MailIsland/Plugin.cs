using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Services;
using MailIsland.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MailIsland;

/// <summary>邮箱插件入口。</summary>
public partial class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        // 初始化运行时目录
        GlobalConstants.PluginConfigFolder = PluginConfigFolder;
        GlobalConstants.PluginFolder = Info.PluginFolderPath;

        // 基础服务
        services.AddSingleton<MailIslandConfigHandler>();
        services.AddSingleton<IMailClientService, MailClientService>();
        services.AddSingleton<MailNotificationProvider>();
        services.AddSingleton<MailPollingService>();
        services.AddHostedService(sp => sp.GetRequiredService<MailPollingService>());

        // 注册提醒提供方（会自动注册为托管服务）
        services.AddNotificationProvider<MailNotificationProvider>();

        // 注册自动化触发器
        services.AddTrigger<Services.Automation.Triggers.NewMailTrigger, Controls.TriggerSettingsControls.NewMailTriggerSettingsControl>();

        // 注册设置页
        services.AddSettingsPage<SettingsPage.MailIslandSettingsPage>();

        // 应用停止时保存配置
        AppBase.Current.AppStopping += (_, _) =>
        {
            IAppHost.GetService<MailIslandConfigHandler>()?.Save();
        };
    }
}