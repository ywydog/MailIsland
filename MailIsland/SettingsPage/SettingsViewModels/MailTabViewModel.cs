using System.Collections.ObjectModel;
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

    private CancellationTokenSource? _loadCts;

    [ObservableProperty] private MailAccountSettings? _selectedAccount;
    [ObservableProperty] private MailMessage? _selectedMessage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Control? _bodyContent;

    public MailTabViewModel(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailCacheStore cache)
    {
        _config = config;
        _mail = mail;
        _cache = cache;
        Accounts = new ReadOnlyCollection<MailAccountSettings>(
            config.Data.Accounts.Where(a => a.Enabled).ToList());
        Messages = new ObservableCollection<MailMessage>();
    }

    public IReadOnlyList<MailAccountSettings> Accounts { get; }
    public ObservableCollection<MailMessage> Messages { get; }

    partial void OnSelectedAccountChanged(MailAccountSettings? value)
    {
        SelectedMessage = null;
        BodyContent = null;

        // 切换账号时取消上一个账号尚未完成的加载，避免结果串号
        _loadCts?.Cancel();
        if (value != null) _ = LoadAsync();
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

            Fill(list);
            Status = $"共 {Messages.Count} 封邮件";
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
                Fill(cached);
                Status = $"无法连接服务器，显示本地缓存（{Messages.Count} 封）：{ex.Message}";
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

    private void Fill(IReadOnlyList<MailMessage> mails)
    {
        Messages.Clear();
        foreach (var m in mails) Messages.Add(m);
    }

    partial void OnSelectedMessageChanged(MailMessage? value)
    {
        BodyContent = null;
        if (value == null) return;

        var body = value.HasHtml ? value.HtmlBody : null;
        BodyContent = HtmlToAvaloniaRenderer.Build(body, value.TextBody);
    }
}
