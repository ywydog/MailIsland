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

        var mask = NotificationContent.CreateSimpleTextContent(title);

        // 正文层模板为单行 + CharacterEllipsis，且容器高度约 40px，
        // 因此内容需压成单行；同时不朗读邮件正文，避免语音泄露邮件内容。
        var overlay = NotificationContent.CreateSimpleTextContent(body);
        overlay.IsSpeechEnabled = false;
        overlay.Duration = TimeSpan.FromSeconds(8);

        var request = new NotificationRequest
        {
            MaskContent = mask,
            OverlayContent = overlay,
        };

        ShowNotification(request);
    }

    /// <summary>根据用户设置构建正文层文本（单行，超长截断）。</summary>
    private static string BuildOverlayBody(MailMessage mail, OverlayContentKind overlayKind)
    {
        // 选择邮件正文且正文不为空时显示正文摘要
        if (overlayKind == OverlayContentKind.Body && !string.IsNullOrWhiteSpace(mail.TextBody))
        {
            return ToSingleLine(mail.TextBody);
        }

        var sender = string.IsNullOrWhiteSpace(mail.SenderName)
            ? mail.SenderEmail
            : $"{mail.SenderName} <{mail.SenderEmail}>";
        return ToSingleLine(string.IsNullOrWhiteSpace(mail.Subject)
            ? sender
            : $"{sender} · {mail.Subject}");
    }

    /// <summary>把任意文本压成单行并截断，避免正文层被裁切后只剩少量字符。</summary>
    private static string ToSingleLine(string text, int maxLength = 160)
    {
        var normalized = string.Join(' ',
            text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));

        if (normalized.Length <= maxLength) return normalized;
        return normalized[..maxLength] + "…";
    }
}