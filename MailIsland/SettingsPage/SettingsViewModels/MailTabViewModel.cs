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
    private readonly MailPollingService _polling;
    private readonly MailCacheStore _cache;

    [ObservableProperty] private MailAccountSettings? _selectedAccount;
    [ObservableProperty] private MailMessage? _selectedMessage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Control? _bodyContent;

    public MailTabViewModel(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailPollingService polling,
        MailCacheStore cache)
    {
        _config = config;
        _mail = mail;
        _polling = polling;
        _cache = cache;
        Messages = new ObservableCollection<MailMessage>();
    }

    public ObservableCollection<MailAccountSettings> Accounts => new(_config.Data.Accounts.Where(a => a.Enabled));
    public ObservableCollection<MailMessage> Messages { get; }

    partial void OnSelectedAccountChanged(MailAccountSettings? value)
    {
        SelectedMessage = null;
        BodyContent = null;
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

        IsLoading = true;
        Status = "正在加载邮件…";
        try
        {
            var plainPassword = _config.Protector.Decrypt(acc.EncryptedPassword);
            var list = await _mail.FetchRecentAsync(acc, plainPassword, _config.Data.MailCountLimit, ct);
            Fill(list);
            Status = $"共 {Messages.Count} 封邮件";
            await _cache.SaveAsync(acc.Id, list, ct);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            // 网络/服务器不可用时回退到上次成功拉取的本地缓存
            var cached = await _cache.TryLoadAsync(acc.Id, ct);
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
            IsLoading = false;
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