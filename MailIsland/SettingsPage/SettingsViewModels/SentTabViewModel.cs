using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.Storage;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>发件箱页 ViewModel。</summary>
public partial class SentTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly Dictionary<string, byte[]?> _inlineImageCache = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _loadCts;

    [ObservableProperty] private MailAccountSettings? _selectedAccount;
    [ObservableProperty] private MailMessage? _selectedMessage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Control? _bodyContent;

    public SentTabViewModel(MailIslandConfigHandler config, IMailClientService mail)
    {
        _config = config;
        _mail = mail;

        Accounts = new ReadOnlyCollection<MailAccountSettings>(
            config.Data.Accounts.Where(a => a.Enabled).ToList());
        Messages = new ObservableCollection<MailMessage>();
        Attachments = new ObservableCollection<MailAttachmentInfo>();

        if (Accounts.Count == 0)
        {
            Status = "还没有可用的邮箱账号，请先到“账号”页添加。";
        }
    }

    public IReadOnlyList<MailAccountSettings> Accounts { get; }
    public ObservableCollection<MailMessage> Messages { get; }
    public ObservableCollection<MailAttachmentInfo> Attachments { get; }

    partial void OnSelectedAccountChanged(MailAccountSettings? value)
    {
        SelectedMessage = null;
        BodyContent = null;
        Attachments.Clear();
        Messages.Clear();
        _inlineImageCache.Clear();

        _loadCts?.Cancel();

        if (value == null)
        {
            Status = "请选择一个账号。";
            return;
        }

        _ = LoadAsync();
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (SelectedAccount is not { } account) return;
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
        {
            Status = "该账号尚未设置授权码。";
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loadCts = cts;

        var accountId = account.Id;
        IsLoading = true;
        Status = "正在加载已发送邮件…";
        try
        {
            var plainPassword = _config.Protector.Decrypt(account.EncryptedPassword);
            var list = await _mail.FetchSentAsync(account, plainPassword, _config.Data.MailCountLimit, cts.Token);

            if (cts.IsCancellationRequested || SelectedAccount?.Id != accountId) return;

            Messages.Clear();
            foreach (var message in list) Messages.Add(message);
            Status = list.Count == 0 ? "已发送文件夹为空。" : $"共 {list.Count} 封已发送邮件";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested || SelectedAccount?.Id != accountId) return;
            Status = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts)) IsLoading = false;
        }
    }

    partial void OnSelectedMessageChanged(MailMessage? value)
    {
        BodyContent = null;
        Attachments.Clear();
        _inlineImageCache.Clear();
        if (value == null) return;

        foreach (var attachment in value.Attachments)
        {
            Attachments.Add(attachment);
        }

        var body = value.HasHtml ? value.HtmlBody : null;
        BodyContent = HtmlToAvaloniaRenderer.Build(
            body, value.TextBody, _config.Data.LoadBodyImages, CreateInlineResolver(value));
    }

    /// <summary>下载附件到桌面。</summary>
    public async Task DownloadAttachmentAsync(MailAttachmentInfo attachment, CancellationToken ct = default)
    {
        if (SelectedAccount is not { } account || SelectedMessage is not { } mail)
        {
            Status = "请先选择邮件。";
            return;
        }
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
        {
            Status = "该账号尚未设置授权码。";
            return;
        }

        try
        {
            Status = $"正在下载 {attachment.FileName}…";
            var plainPassword = _config.Protector.Decrypt(account.EncryptedPassword);
            var bytes = await _mail.DownloadAttachmentAsync(
                account, plainPassword, mail.Uid, attachment.PartSpecifier, ct);

            var directory = AttachmentStorage.GetDownloadDirectory();
            var path = AttachmentStorage.BuildTargetPath(directory, attachment.FileName);
            await File.WriteAllBytesAsync(path, bytes, ct);

            Status = $"已保存到桌面：{Path.GetFileName(path)}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Status = $"下载失败：{ex.Message}";
        }
    }

    private async Task<byte[]?> LoadInlineImageAsync(MailMessage mail, string contentId)
    {
        var normalized = MimeContentId.Normalize(contentId);
        if (!mail.InlineParts.TryGetValue(normalized, out var partSpecifier)) return null;
        if (_inlineImageCache.TryGetValue(partSpecifier, out var cached)) return cached;

        if (SelectedAccount is not { } account) return null;
        if (string.IsNullOrWhiteSpace(account.EncryptedPassword)) return null;

        try
        {
            var plainPassword = _config.Protector.Decrypt(account.EncryptedPassword);
            var bytes = await _mail.DownloadAttachmentAsync(
                account, plainPassword, mail.Uid, partSpecifier, CancellationToken.None);

            _inlineImageCache[partSpecifier] = bytes;
            return bytes;
        }
        catch
        {
            return null;
        }
    }

    private InlineImageResolver? CreateInlineResolver(MailMessage mail) =>
        mail.InlineParts.Count == 0
            ? null
            : new InlineImageResolver(mail.InlineParts, cid => LoadInlineImageAsync(mail, cid));
}
