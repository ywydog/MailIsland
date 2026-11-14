using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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

/// <summary>“将屏幕截图发送”自动化行动：捕获全部显示器并作为 PNG 附件发送。</summary>
[ActionInfo(GlobalConstants.SendScreenshotActionId, "将屏幕截图发送", "\uEEE7", addDefaultToMenu: false)]
public class SendScreenshotAction(
    ILogger<SendScreenshotAction> logger,
    MailIslandConfigHandler config,
    ISmtpClientService smtp,
    IMailClientService mail) : ActionBase<SendScreenshotActionSettings>
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    protected override async Task OnInvoke()
    {
        await base.OnInvoke();

        var account = ResolveAccount()
                      ?? throw new InvalidOperationException("屏幕截图发送：没有可用的邮箱账号，请先在“账号”页添加。");
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
        {
            throw new InvalidOperationException("屏幕截图发送：该账号尚未设置授权码。");
        }

        var to = AddressListParser.Parse(Settings.To);
        if (to.Addresses.Count == 0)
        {
            throw new InvalidOperationException("屏幕截图发送：请填写至少一个有效的收件人。");
        }
        if (to.HasInvalid)
        {
            throw new InvalidOperationException($"屏幕截图发送：收件人格式不正确：{string.Join("、", to.Invalid)}");
        }

        using var bitmap = CaptureScreen();
        using var imageStream = new MemoryStream();
        bitmap.Save(imageStream, ImageFormat.Png);
        imageStream.Position = 0;

        var message = BuildMessage(account, bitmap, imageStream);
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
                logger.LogWarning(ex, "屏幕截图发送：已发送，但保存到“已发送”失败");
            }
        }

        logger.LogInformation("屏幕截图已发送（{Width}×{Height}）", bitmap.Width, bitmap.Height);
    }

    private static Bitmap CaptureScreen()
    {
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("屏幕截图发送：无法获取屏幕尺寸。");
        }

        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private MimeMessage BuildMessage(MailAccountSettings account, Bitmap bitmap, Stream imageStream)
    {
        var now = DateTime.Now;
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(account.DisplayName, account.Email));

        foreach (var entry in AddressListParser.Parse(Settings.To).Addresses)
        {
            message.To.Add(new MailboxAddress(entry.Name, entry.Address));
        }

        message.Subject = string.IsNullOrWhiteSpace(Settings.Subject)
            ? $"屏幕截图 {now:yyyy-MM-dd HH:mm:ss}"
            : Settings.Subject.Trim();
        message.Date = now;
        message.MessageId = MimeUtils.GenerateMessageId();

        var body = new TextPart("plain")
        {
            Text = $"截图时间：{now:yyyy-MM-dd HH:mm:ss}\n分辨率：{bitmap.Width} × {bitmap.Height}",
        };
        var attachment = new MimePart("image", "png")
        {
            Content = new MimeContent(imageStream),
            ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
            ContentTransferEncoding = ContentEncoding.Base64,
            FileName = $"screenshot-{now:yyyyMMdd-HHmmss}.png",
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

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
