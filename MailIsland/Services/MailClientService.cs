using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using MailIsland.Logic.Config;
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

    /// <summary>拉取最近 count 封邮件（含正文 Html/Text）。</summary>
    public async Task<List<MailMessage>> FetchRecentAsync(
        MailAccountSettings account, string plainPassword, int count, CancellationToken ct)
    {
        var result = new List<MailMessage>();

        using var client = new ImapClient();
        await client.ConnectAsync(account.ImapServer, account.ImapPort,
            account.ImapUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls, ct);
        await client.AuthenticateAsync(account.Email, plainPassword, ct);

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
                var subject = s.Envelope?.Subject;
                if (string.IsNullOrWhiteSpace(subject)) subject = "(无主题)";

                var message = new MailMessage
                {
                    Uid = s.UniqueId.Id,
                    SenderName = s.Envelope?.From.Mailboxes.FirstOrDefault()?.Name ?? "",
                    SenderEmail = s.Envelope?.From.Mailboxes.FirstOrDefault()?.Address ?? "",
                    Subject = subject,
                    Date = s.Envelope?.Date?.ToOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue,
                    IsUnread = !(s.Flags?.HasFlag(MessageFlags.Seen) ?? false),
                    HasAttachment = s.Attachments.Any(),
                };

                // 拉取正文：有 HTML 用 HTML，否则用纯文本
                if (s.HtmlBody != null)
                {
                    var htmlEntity = await inbox.GetBodyPartAsync(s.UniqueId, s.HtmlBody, ct);
                    if (htmlEntity is TextPart htmlText)
                    {
                        message.HtmlBody = htmlText.Text;
                        message.HasHtml = true;
                    }
                }
                if (!message.HasHtml && s.TextBody != null)
                {
                    var textEntity = await inbox.GetBodyPartAsync(s.UniqueId, s.TextBody, ct);
                    if (textEntity is TextPart text)
                        message.TextBody = text.Text;
                }

                result.Add(message);
            }
        }

        await client.DisconnectAsync(true, ct);
        return result;
    }
}