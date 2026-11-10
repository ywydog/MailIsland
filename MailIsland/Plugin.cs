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
        services.AddSingleton<ISmtpClientService, SmtpClientService>();
        services.AddSingleton<Storage.MailCacheStore>();
        services.AddSingleton<MailNotificationProvider>();
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

        // 注册设置页分组（折叠下方设置页为一个“邮箱设置”分组）
        services.AddSettingsPageGroup(Shared.GlobalConstants.SettingsGroupId, "\uE8B7", "邮箱设置");

        // 注册设置页（多个独立设置页，[Group] 特性自动归入上述分组）
        services.AddSettingsPage<SettingsPage.Views.AccountTabPage>();
        services.AddSettingsPage<SettingsPage.Views.MailTabPage>();
        services.AddSettingsPage<SettingsPage.Views.SentTabPage>();
        services.AddSettingsPage<SettingsPage.Views.NotifyTabPage>();

        // 应用停止时保存配置
        AppBase.Current.AppStopping += (_, _) =>
        {
            IAppHost.GetService<MailIslandConfigHandler>()?.Save();
        };
    }
}