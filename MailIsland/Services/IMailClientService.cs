using MailIsland.Logic.Config;
using MailIsland.Models;

namespace MailIsland.Services;

public interface IMailClientService
{
    /// <summary>校验账号连接（IMAP + 认证）。</summary>
    Task VerifyConnectionAsync(MailAccountSettings account, string plainPassword, CancellationToken ct);

    /// <summary>拉取最近 count 封邮件。</summary>
    Task<List<MailMessage>> FetchRecentAsync(MailAccountSettings account, string plainPassword, int count, CancellationToken ct);
}