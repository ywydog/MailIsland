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
        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        var result = await FetchFromFolderAsync(inbox, count, includeBodies, ct);

        await client.DisconnectAsync(true, ct);
        return result;
    }

    /// <summary>拉取“已发送”文件夹中的最近邮件。</summary>
    public async Task<List<MailMessage>> FetchSentAsync(
        MailAccountSettings account, string plainPassword, int count, CancellationToken ct)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var sent = await FindSentFolderAsync(client, ct)
                   ?? throw new InvalidOperationException("未找到“已发送”文件夹。");

        await sent.OpenAsync(FolderAccess.ReadOnly, ct);
        var result = await FetchFromFolderAsync(sent, count, includeBodies: true, ct);

        await client.DisconnectAsync(true, ct);
        return result;
    }

    private static async Task<List<MailMessage>> FetchFromFolderAsync(
        IMailFolder folder, int count, bool includeBodies, CancellationToken ct)
    {
        var result = new List<MailMessage>();

        var total = folder.Count;
        var startIndex = Math.Max(0, total - Math.Max(0, count));

        // 无邮件时避免越界
        if (startIndex >= total) return result;

        var fetchItems = MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope |
                         MessageSummaryItems.Flags | MessageSummaryItems.BodyStructure;
        var summaries = await folder.FetchAsync(startIndex, total - 1, fetchItems, ct);

        foreach (var summary in summaries.OrderByDescending(s => s.UniqueId))
        {
            var message = CreateMessage(summary);
            if (includeBodies)
            {
                await PopulateBodyAsync(folder, summary, message, ct);
            }
            result.Add(message);
        }

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

    /// <summary>下载指定邮件的单个附件。</summary>
    public async Task<byte[]> DownloadAttachmentAsync(
        MailAccountSettings account, string plainPassword, uint uid, string partSpecifier, CancellationToken ct)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

        // GetBodyPart 需要 BODYSTRUCTURE 中的部件对象，先按 UID 取回再定位
        var summaries = await inbox.FetchAsync(
            new[] { new UniqueId(uid) }, MessageSummaryItems.UniqueId | MessageSummaryItems.BodyStructure, ct);
        var body = summaries.FirstOrDefault()?.Body;
        var bodyPart = body == null ? null : FindBodyPart(body, partSpecifier);
        if (bodyPart == null)
        {
            throw new InvalidOperationException("未找到该附件，可能已被删除或邮件结构已变化。");
        }

        var entity = await inbox.GetBodyPartAsync(new UniqueId(uid), bodyPart, ct)
                     ?? throw new InvalidOperationException("无法获取附件内容。");
        using var stream = new MemoryStream();

        switch (entity)
        {
            case MessagePart messagePart when messagePart.Message != null:
                await messagePart.Message.WriteToAsync(stream, ct);
                break;
            case MimePart part when part.Content != null:
                await part.Content.DecodeToAsync(stream, ct);
                break;
            default:
                throw new InvalidOperationException("该附件类型暂不支持下载。");
        }

        await client.DisconnectAsync(true, ct);
        return stream.ToArray();
    }

    /// <summary>把邮件标记为已读（服务器端 \Seen）。</summary>
    public async Task MarkAsReadAsync(
        MailAccountSettings account, string plainPassword, uint uid, CancellationToken ct)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var inbox = client.Inbox;
        await inbox.OpenAsync(FolderAccess.ReadWrite, ct);
        await inbox.AddFlagsAsync(new UniqueId(uid), MessageFlags.Seen, silent: true, ct);

        await client.DisconnectAsync(true, ct);
    }

    /// <summary>把已发送邮件追加到“已发送”文件夹。</summary>
    public async Task AppendToSentAsync(
        MailAccountSettings account, string plainPassword, MimeMessage message, CancellationToken ct)
    {
        using var client = new ImapClient();
        await ConnectAsync(client, account, plainPassword, ct);

        var sent = await FindSentFolderAsync(client, ct)
                   ?? throw new InvalidOperationException("未找到“已发送”文件夹。");

        await sent.AppendAsync(message, MessageFlags.Seen, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static async Task<IMailFolder?> FindSentFolderAsync(ImapClient client, CancellationToken ct)
    {
        try
        {
            return client.GetFolder(SpecialFolder.Sent);
        }
        catch
        {
            // 优先使用服务器声明的特殊文件夹，失败时按名称回退
        }

        foreach (var ns in client.PersonalNamespaces)
        {
            try
            {
                var folders = await client.GetFoldersAsync(ns, false, ct);
                var match = folders.FirstOrDefault(IsSentFolder);
                if (match != null) return match;
            }
            catch
            {
                // 忽略单个命名空间失败
            }
        }

        return null;
    }

    private static bool IsSentFolder(IMailFolder folder)
    {
        if (folder.Attributes.HasFlag(FolderAttributes.Sent)) return true;

        var name = folder.Name;
        return name.Contains("sent", StringComparison.OrdinalIgnoreCase) ||
               name.Contains("已发送");
    }

    private async Task ConnectAsync(ImapClient client, MailAccountSettings account, string plainPassword, CancellationToken ct)
    {
        // 默认 120 秒过长，单个账号卡住会阻塞整轮同步
        client.Timeout = 30_000;

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

        var attachments = BuildAttachmentInfos(summary);
        var inlineParts = BuildInlineParts(summary);

        return new MailMessage
        {
            Uid = summary.UniqueId.Id,
            SenderName = summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Name ?? "",
            SenderEmail = summary.Envelope?.From.Mailboxes.FirstOrDefault()?.Address ?? "",
            ToDisplay = FormatAddresses(summary.Envelope?.To),
            Subject = subject,
            Date = summary.Envelope?.Date?.ToOffset(TimeSpan.Zero) ?? DateTimeOffset.MinValue,
            IsUnread = !(summary.Flags?.HasFlag(MessageFlags.Seen) ?? false),
            HasAttachment = attachments.Count > 0,
            Attachments = attachments,
            InlineParts = inlineParts,
        };
    }

    /// <summary>把收件人列表格式化为展示文本。</summary>
    private static string FormatAddresses(InternetAddressList? addresses)
    {
        if (addresses == null || addresses.Count == 0) return "";

        return string.Join(", ", addresses.Mailboxes.Select(mailbox =>
            string.IsNullOrWhiteSpace(mailbox.Name)
                ? mailbox.Address
                : $"{mailbox.Name} <{mailbox.Address}>"));
    }

    /// <summary>收集正文内嵌图片（带 Content-ID 的 image/* 部件），供 cid: 引用解析。</summary>
    private static Dictionary<string, string> BuildInlineParts(IMessageSummary summary)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (summary.Body == null) return result;

        foreach (var part in EnumerateParts(summary.Body))
        {
            if (part is not BodyPartBasic basic) continue;
            if (string.IsNullOrWhiteSpace(basic.ContentId)) continue;
            if (!basic.ContentType.MimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) continue;

            var contentId = MimeContentId.Normalize(basic.ContentId);
            if (contentId.Length == 0) continue;

            result[contentId] = basic.PartSpecifier;
        }

        return result;
    }

    private static IEnumerable<BodyPart> EnumerateParts(BodyPart part)
    {
        yield return part;

        switch (part)
        {
            case BodyPartMultipart multipart:
                foreach (var child in multipart.BodyParts)
                {
                    foreach (var nested in EnumerateParts(child)) yield return nested;
                }
                break;
            case BodyPartMessage message when message.Body != null:
                foreach (var nested in EnumerateParts(message.Body)) yield return nested;
                break;
        }
    }

    /// <summary>从 BODYSTRUCTURE 提取附件元数据，下载时再按 PartSpecifier 拉取内容。</summary>
    private static List<MailAttachmentInfo> BuildAttachmentInfos(IMessageSummary summary)
    {
        var result = new List<MailAttachmentInfo>();
        var index = 0;

        foreach (var part in summary.Attachments)
        {
            index++;

            var fileName = part.FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                var extension = MimeTypes.TryGetExtension(part.ContentType.MimeType, out var ext) ? ext : ".dat";
                fileName = $"attachment-{index}{extension}";
            }

            result.Add(new MailAttachmentInfo
            {
                FileName = fileName,
                ContentType = part.ContentType.MimeType,
                Size = (long)part.Octets,
                PartSpecifier = part.PartSpecifier,
            });
        }

        return result;
    }

    /// <summary>按 PartSpecifier 在 BODYSTRUCTURE 中递归查找部件。</summary>
    private static BodyPart? FindBodyPart(BodyPart part, string partSpecifier)
    {
        if (string.Equals(part.PartSpecifier, partSpecifier, StringComparison.Ordinal))
        {
            return part;
        }

        switch (part)
        {
            case BodyPartMultipart multipart:
                foreach (var child in multipart.BodyParts)
                {
                    var found = FindBodyPart(child, partSpecifier);
                    if (found != null) return found;
                }
                break;
            case BodyPartMessage message when message.Body != null:
                return FindBodyPart(message.Body, partSpecifier);
        }

        return null;
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
