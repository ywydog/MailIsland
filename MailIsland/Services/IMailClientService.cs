using MailIsland.Logic.Config;
using MailIsland.Models;

namespace MailIsland.Services;

public interface IMailClientService
{
    /// <summary>校验账号连接（IMAP + 认证）。</summary>
    Task VerifyConnectionAsync(MailAccountSettings account, string plainPassword, CancellationToken ct);

    /// <summary>拉取最近 count 封邮件；<paramref name="includeBodies"/> 为 false 时只取摘要。</summary>
    Task<List<MailMessage>> FetchRecentAsync(
        MailAccountSettings account, string plainPassword, int count, bool includeBodies, CancellationToken ct);

    /// <summary>为已拉取的邮件补充正文（仅对需要推送的新邮件调用，避免每轮全量下载）。</summary>
    Task FillBodiesAsync(
        MailAccountSettings account, string plainPassword, IReadOnlyList<MailMessage> mails, CancellationToken ct);
}
