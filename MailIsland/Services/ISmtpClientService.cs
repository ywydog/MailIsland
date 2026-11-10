using MailIsland.Logic.Config;
using MimeKit;

namespace MailIsland.Services;

/// <summary>SMTP 发信服务。</summary>
public interface ISmtpClientService
{
    /// <summary>通过账号配置的 SMTP 服务器发送邮件。</summary>
    Task SendAsync(
        MailAccountSettings account, string plainPassword, MimeMessage message, CancellationToken ct);

    /// <summary>校验 SMTP 连接与认证（不发送邮件）。</summary>
    Task VerifyConnectionAsync(
        MailAccountSettings account, string plainPassword, CancellationToken ct);
}
