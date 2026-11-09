using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MailIsland.Logic.Config;
using MailIsland.Logic.Html;
using MailIsland.Models;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MailIsland.Services;

/// <summary>基于 MailKit 的 IMAP 邮件客户端服务。</summary>
public sealed class MailClientService : IMailClientService
{
    private readonly ILogger<MailClientService> _logger;

    public MailClientService(ILogger<MailClientService> logger) => _logger = logger;

    /// <summary>校验账号连接：IMAP 连接 + 认证（授权码）。</summary>
    public async Task VerifyConnectionAsync(MailAccountSettings account, string plainPassword, CancellationToken ct)
    {
        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(account.ImapServer, account.ImapPort,
                account.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(account.Email, plainPassword, ct);
            await TryIdentifyAsync(client, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (AuthenticationException)
        {
            throw new InvalidOperationException("授权码无效或已过期，请检查后重试。");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IMAP 连接验证失败: {Server}", account.ImapServer);
            throw new InvalidOperationException($"无法连接邮件服务器：{ex.Message}");
        }
    }

    /// <summary>拉取最近 count 封邮件；<paramref name="includeBodies"/> 为 false 时只取摘要。</summary>
    public async Task<List<MailMessage>> FetchRecentAsync(
        MailAccountSettings account, string plainPassword, int count, bool includeBodies, CancellationToken ct)
    {
        var result = new List<MailMessage>();

        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        var total = inbox.Count;
        var startIndex = Math.Max(0, total - Math.Max(0, count));

        // 无邮件时避免越界
        if (startIndex < total)
        {
            var fetchItems = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope |
                             MessageSummaryItems.Flags | MessageSummaryItems.BodyStructure;
            var summaries = await inbox.FetchAsync(startIndex, total - 1, fetchItems, ct);

            foreach (var s in summaries.OrderByDescending(s => s.UniqueId))
            {
                var message = CreateMessage(s);
                if (includeBodies)
                {
                    await PopulateBodyAsync(inbox, s, message, ct);
                }
                result.Add(message);
            }
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }

    /// <summary>为指定邮件补充正文（按 UID 精确拉取，避免重复下载所有邮件）。</summary>
    public async Task FillBodiesAsync(
        MailAccountSettings account, string plainPassword, IReadOnlyList<MailMessage> mails, CancellationToken ct)
    {
        if (mails.Count == 0) return;

        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        var byUid = mails.GroupBy(m => m.Uid).ToDictionary(g => g.Key, g => g.First());
        var uids = byUid.Keys.Select(uid => new UniqueId(uid)).ToList();
        var summaries = await inbox.FetchAsync(
            uids, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure, ct);

        foreach (var summary in summaries)
        {
            if (byUid.TryGetValue(summary.UniqueId.Id, out var message))
            {
                await PopulateBodyAsync(inbox, summary, message, ct);
            }
        }

        await client.DisconnectAsync(true, ct);
    }

    private async Task ConnectAsync(ImapClient client, MailAccountSettings account, string plainPassword, CancellationToken ct)
    {
        await client.ConnectAsync(account.ImapServer, account.ImapPort,
            account.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(account.Email, plainPassword, ct);

        // 网易邮箱（163/126/yeah 等）若未在登录后发送 IMAP ID，会以
        // “EXAMINE Unsafe Login” 拒绝打开收件箱（RFC 2971）。
        await TryIdentifyAsync(client, ct);
    }

    private static MailMessage CreateMessage(IMessageSummary summary)
    {
        var subject = summary.Envelope?.Subject;
        if (string.IsNullOrWhiteSpace(subject)) subject = "(无主题)";

        return new MailMessage
        {
            Uid = summary.UniqueId.Id,
            SenderName = summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Name ?? "",
            SenderEmail = summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Address ?? "",
            Subject = subject,
            Date = summary.Envelope?.Date?.ToOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue,
            IsUnread = !(summary.Flags?.HasFlag(MessageFlags.Seen) ?? false),
            HasAttachment = summary.Attachments.Any(),
        };
    }

    /// <summary>拉取正文：优先 HTML；同时派生平文本，供关键词匹配与提醒正文使用。</summary>
    private static async Task PopulateBodyAsync(
        IMailFolder inbox, IMessageSummary summary, MailMessage message, CancellationToken ct)
    {
        if (summary.HtmlBody != null)
        {
            var htmlEntity = await inbox.GetBodyPartAsync(summary.UniqueId, summary.HtmlBody, ct);
            if (htmlEntity is TextPart htmlText)
            {
                message.HtmlBody = htmlText.Text;
                message.HasHtml = true;
                message.TextBody = HtmlSanitizer.ToPlainText(htmlText.Text);
            }
            return;
        }

        if (summary.TextBody != null)
        {
            var textEntity = await inbox.GetBodyPartAsync(summary.UniqueId, summary.TextBody, ct);
            if (textEntity is TextPart text)
            {
                message.TextBody = text.Text;
            }
        }
    }

    /// <summary>
    /// 向服务器发送 IMAP ID（RFC 2971）客户端标识。
    /// <para>网易邮箱（imap.163.com / imap.126.com 等）在未发送 ID 时会以
    /// “EXAMINE Unsafe Login. Please contact kefu@188.com” 拒绝打开文件夹，
    /// 因此必须在登录后、打开收件箱前发送该命令。</para>
    /// </summary>
    private async Task TryIdentifyAsync(ImapClient client, CancellationToken ct)
    {
        if (!client.Capabilities.HasFlag(ImapCapabilities.Id))
            return;

        var implementation = new ImapImplementation
        {
            Name = "MailIsland",
            Version = "1.0.0",
            Vendor = "ywydog",
            SupportUrl = "https://github.com/ywydog/MailIsland",
        };

        try
        {
            await client.IdentifyAsync(implementation, ct);
        }
        catch (Exception ex) when (ex is ImapCommandException or NotSupportedException)
        {
            // 个别服务器宣称支持 ID 却拒绝该命令；此处不阻断流程，
            // 若服务器强制要求 ID，后续打开文件夹时会给出真实错误。
            _logger.LogDebug(ex, "发送 IMAP ID 失败");
        }
    }
}
