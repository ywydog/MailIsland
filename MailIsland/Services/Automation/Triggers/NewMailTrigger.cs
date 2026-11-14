using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using MailIsland.Models;
using MailIsland.Models.Automation;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services.Automation.Triggers;

/// <summary>
/// 新邮件命中关键词自动化触发器。
/// <para>订阅“新邮件到达”事件，按用户配置（标题/正文 + 关键词）判断，命中则触发。</para>
/// </summary>
[TriggerInfo(GlobalConstants.NewMailTriggerId, "新邮件包含关键词", FluentIcons.MailAlertRegular)]
public class NewMailTrigger(
    MailPollingService pollingService,
    ILogger<NewMailTrigger> logger) : TriggerBase<NewMailTriggerSettings>
{
    private MailPollingService PollingService { get; } = pollingService;
    private bool _subscribed;

    /// <inheritdoc />
    public override void Loaded()
    {
        // 幂等订阅，避免宿主重复调用 Loaded 时一次新邮件触发多次
        if (_subscribed) return;

        PollingService.NewMailReceived += OnNewMailReceived;
        _subscribed = true;
    }

    /// <inheritdoc />
    public override void UnLoaded()
    {
        if (!_subscribed) return;

        PollingService.NewMailReceived -= OnNewMailReceived;
        _subscribed = false;
    }

    private void OnNewMailReceived(object? sender, MailMessage mail)
    {
        try
        {
            if (Matches(mail))
            {
                logger.LogDebug("新邮件命中自动化关键词，触发行动");
                Trigger();
            }
        }
        catch (Exception ex)
        {
            // 自动化执行异常不应影响邮件轮询与其他触发器
            logger.LogWarning(ex, "新邮件自动化触发器执行失败");
        }
    }

    private bool Matches(MailMessage mail)
    {
        var keyword = Settings.Keyword?.Trim() ?? "";
        if (keyword.Length == 0)
        {
            return false;
        }

        var text = Settings.Target switch
        {
            MailKeywordTarget.Body => mail.TextBody,
            _ => mail.Subject,
        };
        return text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false;
    }
}