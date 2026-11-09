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
    /// <summary>不超过该长度时直接静态显示，超过则滚动显示。</summary>
    private const int SimpleTextThreshold = 28;

    /// <summary>正文层文本的最大长度（滚动展示，避免过长占用过多时间）。</summary>
    private const int MaxOverlayLength = 240;

    /// <summary>推送新邮件提醒。</summary>
    public void Notify(MailMessage mail, MailAccountSettings account, OverlayContentKind overlayKind, bool keywordHit, string? keyword = null)
    {
        var title = keywordHit && !string.IsNullOrEmpty(keyword)
            ? $"【{keyword}】{account.DisplayName} 新邮件"
            : $"{account.DisplayName} 新邮件";

        var body = BuildOverlayBody(mail, overlayKind);

        var mask = NotificationContent.CreateSimpleTextContent(title);
        var overlay = BuildOverlayContent(body);

        var request = new NotificationRequest
        {
            MaskContent = mask,
            OverlayContent = overlay,
        };

        ShowNotification(request);
    }

    /// <summary>短文本直接显示，长文本使用宿主内置的滚动文本模板横向滚动。</summary>
    private static NotificationContent BuildOverlayContent(string body)
    {
        if (body.Length <= SimpleTextThreshold)
        {
            var simple = NotificationContent.CreateSimpleTextContent(body);
            simple.IsSpeechEnabled = false;
            simple.Duration = TimeSpan.FromSeconds(8);
            return simple;
        }

        // 滚动模板为单行，正好适配正文层约 40px 的高度；
        // 按每秒约 8 个字符估算时长，并限制在 8~30 秒。
        var seconds = Math.Clamp(body.Length / 8.0, 8, 30);
        return NotificationContent.CreateRollingTextContent(
            body, TimeSpan.FromSeconds(seconds), repeatCount: 1,
            content => content.IsSpeechEnabled = false);
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

    /// <summary>把任意文本压成单行并截断，避免正文层出现换行或被裁切。</summary>
    private static string ToSingleLine(string text, int maxLength = MaxOverlayLength)
    {
        var normalized = string.Join(' ',
            text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => part.Length > 0));

        if (normalized.Length <= maxLength) return normalized;
        return normalized[..maxLength] + "…";
    }
}