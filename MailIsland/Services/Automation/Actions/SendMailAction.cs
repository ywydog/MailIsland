using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Mail;
using MailIsland.Models.Automation;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;

namespace MailIsland.Services.Automation.Actions;

/// <summary>“发送邮件”自动化行动。</summary>
[ActionInfo(GlobalConstants.SendMailActionId, "发送邮件", "\uE724", addDefaultToMenu: false)]
public class SendMailAction(
    ILogger<SendMailAction> logger,
    MailIslandConfigHandler config,
    ISmtpClientService smtp,
    IMailClientService mail) : ActionBase<SendMailActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var account = ResolveAccount()
                      ?? throw new InvalidOperationException("发送邮件：没有可用的邮箱账号，请先在“账号”页添加。");
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
        {
            throw new InvalidOperationException("发送邮件：该账号尚未设置授权码。");
        }

        var to = AddressListParser.Parse(Settings.To);
        if (to.Addresses.Count == 0)
        {
            throw new InvalidOperationException("发送邮件：请填写至少一个有效的收件人。");
        }
        if (to.HasInvalid)
        {
            throw new InvalidOperationException($"发送邮件：收件人格式不正确：{string.Join("、", to.Invalid)}");
        }

        var cc = AddressListParser.Parse(Settings.Cc);
        if (cc.HasInvalid)
        {
            throw new InvalidOperationException($"发送邮件：抄送格式不正确：{string.Join("、", cc.Invalid)}");
        }

        var bcc = AddressListParser.Parse(Settings.Bcc);
        if (bcc.HasInvalid)
        {
            throw new InvalidOperationException($"发送邮件：密送格式不正确：{string.Join("、", bcc.Invalid)}");
        }

        var message = BuildMessage(account);
        var plainPassword = config.Protector.Decrypt(account.EncryptedPassword);

        await smtp.SendAsync(account, plainPassword, message, InterruptCancellationToken);

        if (Settings.SaveSentCopy)
        {
            try
            {
                await mail.AppendToSentAsync(account, plainPassword, message, InterruptCancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "发送邮件：已发送，但保存到“已发送”失败");
            }
        }

        logger.LogInformation("自动化发送邮件完成");
    }

    private MailAccountSettings? ResolveAccount()
    {
        var accounts = config.Data.Accounts
            .Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Email))
            .ToList();

        if (accounts.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(Settings.AccountId))
        {
            var match = accounts.FirstOrDefault(a => a.Id == Settings.AccountId);
            if (match != null) return match;
        }

        return accounts[0];
    }

    private MimeMessage BuildMessage(MailAccountSettings account)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.DisplayName, account.Email));

        AddAddresses(message.To, Settings.To);
        AddAddresses(message.Cc, Settings.Cc);
        AddAddresses(message.Bcc, Settings.Bcc);

        message.Subject = Settings.Subject?.Trim() ?? "";
        message.Date = DateTimeOffset.Now;
        message.MessageId = MimeUtils.GenerateMessageId();

        var body = Settings.Body ?? "";
        var signature = account.Signature?.Trim();
        if (!string.IsNullOrWhiteSpace(signature))
        {
            body = string.IsNullOrWhiteSpace(body) ? signature : $"{body}\n\n{signature}";
        }

        message.Body = new TextPart("plain") { Text = body };
        return message;
    }

    private static void AddAddresses(InternetAddressList target, string input)
    {
        foreach (var entry in AddressListParser.Parse(input).Addresses)
        {
            target.Add(new MailboxAddress(entry.Name, entry.Address));
        }
    }
}
