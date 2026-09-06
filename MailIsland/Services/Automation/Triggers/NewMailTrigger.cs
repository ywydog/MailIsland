using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using MailIsland.Models;
using MailIsland.Models.Automation;
using MailIsland.Shared;

namespace MailIsland.Services.Automation.Triggers;

/// <summary>
/// 新邮件命中关键词自动化触发器。
/// <para>订阅“新邮件到达”事件，按用户配置（标题/正文 + 关键词）判断，命中则触发。</para>
/// </summary>
[TriggerInfo(GlobalConstants.NewMailTriggerId, "新邮件包含关键词", "\ue8b7")]
public class NewMailTrigger(MailPollingService pollingService) : TriggerBase<NewMailTriggerSettings>
{
    private MailPollingService PollingService { get; } = pollingService;

    /// <inheritdoc />
    public override void Loaded()
    {
        PollingService.NewMailReceived += OnNewMailReceived;
    }

    /// <inheritdoc />
    public override void UnLoaded()
    {
        PollingService.NewMailReceived -= OnNewMailReceived;
    }

    private void OnNewMailReceived(object? sender, MailMessage mail)
    {
        if (Matches(mail))
        {
            Trigger();
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