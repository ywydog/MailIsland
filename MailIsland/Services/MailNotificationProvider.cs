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
    /// <summary>自动模式下，不超过该长度时静态显示。</summary>
    private const int AutoScrollThreshold = 28;

    /// <summary>正文层文本的最大长度，避免滚动展示时间过长。</summary>
    private const int MaxOverlayLength = 240;

    /// <summary>推送新邮件提醒。</summary>
    public void Notify(
        MailMessage mail,
        MailAccountSettings account,
        MailIslandConfigData config,
        bool keywordHit,
        string? keyword = null)
    {
        var mask = NotificationContent.CreateSimpleTextContent(BuildMaskTitle(mail, account, config.MaskContentKind, keywordHit, keyword));
        mask.IsSpeechEnabled = config.NotificationSpeechEnabled;

        var body = BuildOverlayBody(mail, config.OverlayContentKind);
        var overlay = BuildOverlayContent(body, config);
        overlay.IsSpeechEnabled = config.NotificationSpeechEnabled;

        var request = new NotificationRequest
        {
            MaskContent = mask,
            OverlayContent = overlay,
        };

        ShowNotification(request);
    }

    /// <summary>构建遮罩（标题）文本。</summary>
    private static string BuildMaskTitle(
        MailMessage mail,
        MailAccountSettings account,
        NotificationMaskKind maskKind,
        bool keywordHit,
        string? keyword)
    {
        var content = maskKind switch
        {
            NotificationMaskKind.Subject => string.IsNullOrWhiteSpace(mail.Subject) ? account.DisplayName : mail.Subject,
            NotificationMaskKind.Sender => GetSenderText(mail, account),
            _ => $"{account.DisplayName} 新邮件",
        };

        return keywordHit && !string.IsNullOrEmpty(keyword)
            ? $"【{keyword}】{content}"
            : content;
    }

    /// <summary>短文本静态显示，长文本按用户选择滚动显示。</summary>
    private static NotificationContent BuildOverlayContent(string body, MailIslandConfigData config)
    {
        var useScroll = config.DisplayMode switch
        {
            NotificationDisplayMode.Static => false,
            NotificationDisplayMode.Scroll => true,
            _ => body.Length > AutoScrollThreshold,
        };

        if (!useScroll)
        {
            var content = NotificationContent.CreateSimpleTextContent(body);
            content.Duration = TimeSpan.FromSeconds(Math.Clamp(config.StaticDisplaySeconds, 3, 120));
            return content;
        }

        var charsPerSecond = Math.Clamp(config.ScrollCharsPerSecond, 1, 100);
        var seconds = Math.Clamp(body.Length / (double)charsPerSecond, 5, 60);
        return NotificationContent.CreateRollingTextContent(
            body, TimeSpan.FromSeconds(seconds), repeatCount: 1);
    }

    /// <summary>根据用户设置构建正文层文本（单行，超长截断）。</summary>
    private static string BuildOverlayBody(MailMessage mail, OverlayContentKind overlayKind)
    {
        var sender = GetSenderText(mail, account: null);
        var subject = mail.Subject;

        var text = overlayKind switch
        {
            OverlayContentKind.Subject => subject,
            OverlayContentKind.SenderOnly => sender,
            OverlayContentKind.Body when !string.IsNullOrWhiteSpace(mail.TextBody) => mail.TextBody,
            _ => string.IsNullOrWhiteSpace(subject) ? sender : $"{sender} · {subject}",
        };

        return ToSingleLine(text);
    }

    /// <summary>发件人展示文本；无账号信息时回退到邮箱地址。</summary>
    private static string GetSenderText(MailMessage mail, MailAccountSettings? account)
    {
        if (!string.IsNullOrWhiteSpace(mail.SenderName))
        {
            return string.IsNullOrWhiteSpace(mail.SenderEmail)
                ? mail.SenderName
                : $"{mail.SenderName} <{mail.SenderEmail}>";
        }

        if (!string.IsNullOrWhiteSpace(mail.SenderEmail)) return mail.SenderEmail;
        return account?.DisplayName ?? "";
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
