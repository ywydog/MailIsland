using ClassIsland.Core.Abstractions.Services.NotificationProviders;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Models.Notification;
using MailIsland.Logic.Config;
using MailIsland.Logic.Shared;
using MailIsland.Models;
using MailIsland.Shared;

namespace MailIsland.Services;

/// <summary>
/// 邮箱新邮件提醒提供方。
/// <para>继承 <see cref="NotificationProviderBase"/>，由 [NotificationProviderInfo] 提供元数据；</para>
/// <para>基类的 Name/Description/ProviderGuid 由特性自动填充，无需手动设置。</para>
/// </summary>
[NotificationProviderInfo(
    GlobalConstants.NotificationProviderGuid,
    "邮箱新邮件提醒",
    "\ue8b7",
    "通过邮箱插件拉取新邮件，命中关键词时推送提醒。")]
public sealed class MailNotificationProvider : NotificationProviderBase
{
    /// <summary>推送新邮件提醒。</summary>
    public void Notify(MailMessage mail, MailAccountSettings account, OverlayContentKind overlayKind, bool keywordHit, string? keyword = null)
    {
        var title = keywordHit && !string.IsNullOrEmpty(keyword)
            ? $"【{keyword}】{account.DisplayName} 新邮件"
            : $"{account.DisplayName} 新邮件";

        var body = BuildOverlayBody(mail, overlayKind);

        var request = new NotificationRequest
        {
            MaskContent = NotificationContent.CreateSimpleTextContent(title),
            OverlayContent = NotificationContent.CreateSimpleTextContent(body),
        };

        ShowNotification(request);
    }

    /// <summary>根据用户设置构建正文层文本。</summary>
    private static string BuildOverlayBody(MailMessage mail, OverlayContentKind overlayKind)
    {
        // 选择邮件正文且正文为空时，回退到发件人信息
        if (overlayKind == OverlayContentKind.Body && !string.IsNullOrWhiteSpace(mail.TextBody))
        {
            return mail.TextBody;
        }

        var sender = string.IsNullOrWhiteSpace(mail.SenderName)
            ? mail.SenderEmail
            : $"{mail.SenderName} <{mail.SenderEmail}>";
        return string.IsNullOrWhiteSpace(mail.Subject)
            ? sender
            : $"{sender}\n{mail.Subject}";
    }
}