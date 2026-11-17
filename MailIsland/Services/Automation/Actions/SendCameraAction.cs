using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Icons;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Mail;
using MailIsland.Models.Automation;
using MailIsland.Services.Capture;
using MailIsland.Shared;
using Microsoft.Extensions.Logging;
using MimeKit;
using MimeKit.Utils;

namespace MailIsland.Services.Automation.Actions;

/// <summary>“将摄像头画面发送”自动化行动：抓取一帧摄像头画面并作为 JPEG 附件发送。</summary>
[ActionInfo(GlobalConstants.SendCameraActionId, "将摄像头画面发送", FluentIcons.CameraRegular, addDefaultToMenu: false)]
public class SendCameraAction(
    ILogger<SendCameraAction> logger,
    MailIslandConfigHandler config,
    ISmtpClientService smtp,
    IMailClientService mail) : ActionBase<SendCameraActionSettings>
{
    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var account = ResolveAccount()
                      ?? throw new InvalidOperationException("摄像头画面发送：没有可用的邮箱账号，请先在“账号”页添加。");
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
        {
            throw new InvalidOperationException("摄像头画面发送：该账号尚未设置授权码。");
        }

        var to = AddressListParser.Parse(Settings.To);
        if (to.Addresses.Count == 0)
        {
            throw new InvalidOperationException("摄像头画面发送：请填写至少一个有效的收件人。");
        }
        if (to.HasInvalid)
        {
            throw new InvalidOperationException($"摄像头画面发送：收件人格式不正确：{string.Join("、", to.Invalid)}");
        }

        var photo = await CameraCapture.CapturePhotoAsync(Settings.CameraId, InterruptCancellationToken);
        using var imageStream = new MemoryStream(photo.Bytes);

        var message = BuildMessage(account, photo, imageStream);
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
                logger.LogWarning(ex, "摄像头画面发送：已发送，但保存到“已发送”失败");
            }
        }

        logger.LogInformation("摄像头画面已发送（{Width}×{Height}，{Size} 字节）",
            photo.Width, photo.Height, photo.Bytes.Length);
    }

    private MimeMessage BuildMessage(MailAccountSettings account, CameraPhoto photo, Stream imageStream)
    {
        var now = DateTime.Now;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.DisplayName, account.Email));

        foreach (var entry in AddressListParser.Parse(Settings.To).Addresses)
        {
            message.To.Add(new MailboxAddress(entry.Name, entry.Address));
        }

        message.Subject = string.IsNullOrWhiteSpace(Settings.Subject)
            ? $"摄像头画面 {now:yyyy-MM-dd HH:mm:ss}"
            : Settings.Subject.Trim();
        message.Date = now;
        message.MessageId = MimeUtils.GenerateMessageId();

        var body = new TextPart("plain")
        {
            Text = $"拍摄时间：{now:yyyy-MM-dd HH:mm:ss}\n分辨率：{photo.Width} × {photo.Height}",
        };
        var attachment = new MimePart("image", "jpeg")
        {
            Content = new MimeContent(imageStream),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = $"camera-{now:yyyyMMdd-HHmmss}.jpg",
        };

        message.Body = new Multipart("mixed") { body, attachment };
        return message;
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
}
