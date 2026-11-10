using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Mail;
using MailIsland.Models;
using MailIsland.Services;
using MimeKit;
using MimeKit.Utils;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>写邮件页 ViewModel：校验、附件、发送与保存到已发送。</summary>
public partial class ComposeViewModel : ObservableObject
{
    private const long WarnBytes = 25L * 1024 * 1024;
    private const long MaxBytes = 50L * 1024 * 1024;

    private readonly MailAccountSettings _account;
    private readonly MailIslandConfigHandler _config;
    private readonly ISmtpClientService _smtp;
    private readonly IMailClientService _mail;

    [ObservableProperty] private string _to = "";
    [ObservableProperty] private string _cc = "";
    [ObservableProperty] private string _bcc = "";
    [ObservableProperty] private string _subject = "";
    [ObservableProperty] private string _body = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _saveSentCopy;
    [ObservableProperty] private string _attachmentSummary = "";

    public ComposeViewModel(
        MailAccountSettings account,
        MailIslandConfigHandler config,
        ISmtpClientService smtp,
        IMailClientService mail)
    {
        _account = account;
        _config = config;
        _smtp = smtp;
        _mail = mail;
        _saveSentCopy = config.Data.SaveSentCopy;

        Attachments = new ObservableCollection<ComposeAttachment>();
        Attachments.CollectionChanged += (_, _) => UpdateAttachmentSummary();
    }

    public ObservableCollection<ComposeAttachment> Attachments { get; }

    /// <summary>是否已填写任何内容（用于返回时的放弃确认）。</summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(To) ||
        !string.IsNullOrWhiteSpace(Cc) ||
        !string.IsNullOrWhiteSpace(Bcc) ||
        !string.IsNullOrWhiteSpace(Subject) ||
        !string.IsNullOrWhiteSpace(Body) ||
        Attachments.Count > 0;

    /// <summary>回复/转发时预填内容；签名会自动附加在正文前。</summary>
    public void ApplyPrefill(string? to, string? subject, string? quote)
    {
        if (!string.IsNullOrWhiteSpace(to)) To = to;
        if (!string.IsNullOrWhiteSpace(subject)) Subject = subject;

        var parts = new List<string>();
        var signature = _account.Signature?.Trim();
        if (!string.IsNullOrWhiteSpace(signature)) parts.Add(signature);
        if (!string.IsNullOrWhiteSpace(quote)) parts.Add(quote.Trim());

        Body = string.Join("\n\n", parts);
    }

    public void AddAttachments(IEnumerable<string> paths)
    {
        foreach (var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Attachments.Any(a => string.Equals(a.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) continue;

                Attachments.Add(new ComposeAttachment
                {
                    FilePath = info.FullName,
                    Size = info.Length,
                });
            }
            catch (Exception ex)
            {
                Status = $"无法添加附件“{Path.GetFileName(path)}”：{ex.Message}";
            }
        }
    }

    public void RemoveAttachment(ComposeAttachment attachment) => Attachments.Remove(attachment);

    /// <summary>发送邮件；成功返回 true（保存到已发送失败也视为发送成功）。</summary>
    public async Task<bool> SendAsync(CancellationToken ct = default)
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            Status = errors[0];
            return false;
        }

        if (string.IsNullOrWhiteSpace(_account.EncryptedPassword))
        {
            Status = "该账号尚未设置授权码。";
            return false;
        }

        IsSending = true;
        Status = "正在发送…";
        try
        {
            var plainPassword = _config.Protector.Decrypt(_account.EncryptedPassword);
            var message = BuildMessage();

            await _smtp.SendAsync(_account, plainPassword, message, ct);

            if (SaveSentCopy)
            {
                try
                {
                    await _mail.AppendToSentAsync(_account, plainPassword, message, ct);
                }
                catch
                {
                    Status = "已发送，但未能保存到“已发送”。";
                    return true;
                }
            }

            Status = "已发送";
            return true;
        }
        catch (OperationCanceledException)
        {
            Status = "已取消发送。";
            return false;
        }
        catch (Exception ex)
        {
            Status = ex.Message;
            return false;
        }
        finally
        {
            IsSending = false;
        }
    }

    private void UpdateAttachmentSummary()
    {
        if (Attachments.Count == 0)
        {
            AttachmentSummary = "";
            return;
        }

        var total = TotalAttachmentSize;
        var hint = total > WarnBytes ? "（偏大，可能被服务器拒收）" : "";
        AttachmentSummary = $"共 {Attachments.Count} 个附件，{ByteSize.Format(total)}{hint}";
    }

    private long TotalAttachmentSize => Attachments.Sum(attachment => attachment.Size);

    private List<string> Validate()
    {
        var errors = new List<string>();

        var to = AddressListParser.Parse(To);
        if (to.Addresses.Count == 0) errors.Add("请填写至少一个有效的收件人。");
        if (to.HasInvalid) errors.Add($"收件人格式不正确：{string.Join("、", to.Invalid)}");

        var cc = AddressListParser.Parse(Cc);
        if (cc.HasInvalid) errors.Add($"抄送格式不正确：{string.Join("、", cc.Invalid)}");

        var bcc = AddressListParser.Parse(Bcc);
        if (bcc.HasInvalid) errors.Add($"密送格式不正确：{string.Join("、", bcc.Invalid)}");

        if (TotalAttachmentSize > MaxBytes)
        {
            errors.Add($"附件总大小 {ByteSize.Format(TotalAttachmentSize)}，超过 50 MB 上限，请精简附件。");
        }

        return errors;
    }

    private MimeMessage BuildMessage()
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_account.DisplayName, _account.Email));

        AddAddresses(message.To, To);
        AddAddresses(message.Cc, Cc);
        AddAddresses(message.Bcc, Bcc);

        message.Subject = Subject.Trim();
        message.Date = DateTimeOffset.Now;
        message.MessageId = MimeUtils.GenerateMessageId();

        var builder = new BodyBuilder { TextBody = Body ?? "" };
        foreach (var attachment in Attachments)
        {
            try
            {
                builder.Attachments.Add(attachment.FilePath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"无法读取附件“{attachment.FileName}”：{ex.Message}");
            }
        }

        message.Body = builder.ToMessageBody();
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
