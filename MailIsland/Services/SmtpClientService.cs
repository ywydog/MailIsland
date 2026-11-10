using System.Net.Sockets;
using MailIsland.Logic.Config;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace MailIsland.Services;

/// <summary>基于 MailKit 的 SMTP 发信服务，强制 TLS 并把常见错误翻译为中文提示。</summary>
public sealed class SmtpClientService : ISmtpClientService
{
    private readonly ILogger<SmtpClientService> _logger;

    public SmtpClientService(ILogger<SmtpClientService> logger) => _logger = logger;

    public async Task SendAsync(
        MailAccountSettings account, string plainPassword, MimeMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(account.SmtpServer))
        {
            throw new InvalidOperationException("该账号未设置 SMTP 服务器，请到“账号”页填写或重新选择邮箱预设。");
        }

        try
        {
            using var client = new SmtpClient();
            client.Timeout = 30_000;

            await client.ConnectAsync(
                account.SmtpServer,
                account.SmtpPort,
                account.SmtpUseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls,
                ct);

            await client.AuthenticateAsync(account.Email, plainPassword, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (AuthenticationException)
        {
            throw new InvalidOperationException("SMTP 认证失败：请检查授权码，并确认邮箱已开启 SMTP 服务。");
        }
        catch (SmtpCommandException ex)
        {
            _logger.LogWarning(ex, "SMTP 发送被拒绝，状态码 {Code}", (int)ex.StatusCode);
            throw new InvalidOperationException(Describe(ex));
        }
        catch (SmtpProtocolException ex)
        {
            _logger.LogWarning(ex, "SMTP 协议错误");
            throw new InvalidOperationException($"SMTP 协议错误：{ex.Message}");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or SocketException or TimeoutException)
        {
            _logger.LogWarning(ex, "SMTP 连接失败");
            throw new InvalidOperationException($"无法连接发件服务器：{ex.Message}");
        }
    }

    private static string Describe(SmtpCommandException ex) => (int)ex.StatusCode switch
    {
        535 => "SMTP 认证失败：请检查授权码，并确认邮箱已开启 SMTP 服务。",
        550 or 553 or 501 => "发件地址或收件人被服务器拒绝，请确认发件人与登录账号一致。",
        552 or 452 => "邮件超出服务器容量限制，请减小附件后重试。",
        421 or 450 or 451 => "服务器暂时限制发送，请稍后再试。",
        _ => $"服务器拒绝发送（{(int)ex.StatusCode}）：{ex.Message}",
    };
}
