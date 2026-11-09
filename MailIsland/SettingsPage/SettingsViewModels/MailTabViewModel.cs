using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.Storage;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>邮件页 ViewModel。</summary>
public partial class MailTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly MailCacheStore _cache;
    private readonly List<MailMessage> _allMails = new();
    private readonly Dictionary<string, byte[]?> _inlineImageCache = new(StringComparer.OrdinalIgnoreCase);

    private CancellationTokenSource? _loadCts;

    [ObservableProperty] private MailAccountSettings? _selectedAccount;
    [ObservableProperty] private MailMessage? _selectedMessage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Control? _bodyContent;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _loadBodyImages;
    [ObservableProperty] private string? _lastSavedFilePath;
    [ObservableProperty] private bool _hasSavedAttachment;

    public MailTabViewModel(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailCacheStore cache)
    {
        _config = config;
        _mail = mail;
        _cache = cache;
        _loadBodyImages = config.Data.LoadBodyImages;

        Accounts = new ReadOnlyCollection<MailAccountSettings>(
            config.Data.Accounts.Where(a => a.Enabled).ToList());
        Messages = new ObservableCollection<MailMessage>();
        Attachments = new ObservableCollection<MailAttachmentInfo>();

        if (Accounts.Count == 0)
        {
            Status = "还没有可用的邮箱账号，请先到“账号”页添加并填写授权码。";
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
        _allMails.Clear();
        _inlineImageCache.Clear();
        HasSavedAttachment = false;
        LastSavedFilePath = null;

        // 切换账号时取消上一个账号尚未完成的加载，避免结果串号
        _loadCts?.Cancel();

        if (value == null)
        {
            Status = "请选择一个收件账号。";
            return;
        }

        _ = LoadAsync();
    }

    /// <summary>按发件人、主题或正文过滤当前邮件列表。</summary>
    partial void OnSearchTextChanged(string value) => ApplyFilter();

    /// <summary>切换正文图片加载开关并重建当前正文。</summary>
    partial void OnLoadBodyImagesChanged(bool value)
    {
        _config.Data.LoadBodyImages = value;
        _config.Save();

        if (SelectedMessage is { } mail)
        {
            var body = mail.HasHtml ? mail.HtmlBody : null;
            BodyContent = HtmlToAvaloniaRenderer.Build(
                body, mail.TextBody, value, CreateInlineResolver(mail));
        }
    }

    /// <summary>按需下载并缓存正文内嵌图片（cid: 引用）。</summary>
    private async Task<byte[]?> LoadInlineImageAsync(MailMessage mail, string contentId)
    {
        var normalized = MimeContentId.Normalize(contentId);
        if (!mail.InlineParts.TryGetValue(normalized, out var partSpecifier)) return null;
        if (_inlineImageCache.TryGetValue(partSpecifier, out var cached)) return cached;

        if (SelectedAccount is not { } acc) return null;
        if (string.IsNullOrWhiteSpace(acc.EncryptedPassword)) return null;

        try
        {
            var plainPassword = _config.Protector.Decrypt(acc.EncryptedPassword);
            var bytes = await _mail.DownloadAttachmentAsync(
                acc, plainPassword, mail.Uid, partSpecifier, CancellationToken.None);

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

    /// <summary>下载附件到桌面（重名自动追加序号）。</summary>
    public async Task DownloadAttachmentAsync(MailAttachmentInfo attachment, CancellationToken ct = default)
    {
        if (SelectedAccount is not { } acc || SelectedMessage is not { } mail)
        {
            Status = "请先选择邮件。";
            return;
        }
        if (string.IsNullOrWhiteSpace(acc.EncryptedPassword))
        {
            Status = "该账号尚未设置授权码。";
            return;
        }

        try
        {
            Status = $"正在下载 {attachment.FileName}…";
            var plainPassword = _config.Protector.Decrypt(acc.EncryptedPassword);
            var bytes = await _mail.DownloadAttachmentAsync(
                acc, plainPassword, mail.Uid, attachment.PartSpecifier, ct);

            var directory = AttachmentStorage.GetDownloadDirectory();
            var path = AttachmentStorage.BuildTargetPath(directory, attachment.FileName);
            await File.WriteAllBytesAsync(path, bytes, ct);

            LastSavedFilePath = path;
            HasSavedAttachment = true;
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

    /// <summary>在资源管理器中定位最近保存的附件。</summary>
    public void OpenContainingFolder()
    {
        if (string.IsNullOrWhiteSpace(LastSavedFilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{LastSavedFilePath}\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Status = $"无法打开文件夹：{ex.Message}";
        }
    }

    /// <summary>拉取所选账号的最近邮件。</summary>
    public async Task LoadAsync(CancellationToken ct = default)
    {
        if (SelectedAccount is not { } acc) return;
        if (string.IsNullOrWhiteSpace(acc.EncryptedPassword))
        {
            Status = "该账号尚未设置授权码。";
            return;
        }

        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loadCts = cts;

        var accountId = acc.Id;
        IsLoading = true;
        Status = "正在加载邮件…";
        try
        {
            var plainPassword = _config.Protector.Decrypt(acc.EncryptedPassword);
            var list = await _mail.FetchRecentAsync(
                acc, plainPassword, _config.Data.MailCountLimit, includeBodies: true, cts.Token);

            // 请求期间账号已切换则丢弃结果
            if (cts.IsCancellationRequested || SelectedAccount?.Id != accountId) return;

            SetMails(list);
            UpdateLoadStatus(list.Count);
            await _cache.SaveAsync(accountId, list, cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (cts.IsCancellationRequested || SelectedAccount?.Id != accountId) return;

            // 网络/服务器不可用时回退到上次成功拉取的本地缓存
            var cached = await _cache.TryLoadAsync(accountId, cts.Token);
            if (cached is { Count: > 0 })
            {
                SetMails(cached);
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    Status = $"无法连接服务器，显示本地缓存（{cached.Count} 封）：{ex.Message}";
                }
            }
            else
            {
                Status = ex.Message;
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts)) IsLoading = false;
        }
    }

    private void SetMails(IReadOnlyList<MailMessage> mails)
    {
        _allMails.Clear();
        _allMails.AddRange(mails);
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchText?.Trim() ?? "";
        var items = query.Length == 0
            ? _allMails
            : _allMails.Where(m => Matches(m, query)).ToList();

        Messages.Clear();
        foreach (var message in items) Messages.Add(message);

        if (query.Length > 0)
        {
            Status = items.Count == 0
                ? $"没有匹配“{query}”的邮件。"
                : $"找到 {items.Count} 封匹配邮件（共 {_allMails.Count} 封）。";
        }
    }

    private void UpdateLoadStatus(int total)
    {
        if (!string.IsNullOrWhiteSpace(SearchText)) return;

        Status = total == 0 ? "该账号暂无邮件。" : $"共 {total} 封邮件";
    }

    private static bool Matches(MailMessage mail, string query) =>
        Contains(mail.Subject, query) ||
        Contains(mail.SenderName, query) ||
        Contains(mail.SenderEmail, query) ||
        Contains(mail.TextBody, query);

    private static bool Contains(string? text, string query) =>
        !string.IsNullOrEmpty(text) && text.Contains(query, StringComparison.OrdinalIgnoreCase);

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
            body, value.TextBody, LoadBodyImages, CreateInlineResolver(value));
    }
}
