using System.Collections.ObjectModel;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>邮件页 ViewModel。</summary>
public partial class MailTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly MailPollingService _polling;

    [ObservableProperty] private MailAccountSettings? _selectedAccount;
    [ObservableProperty] private MailMessage? _selectedMessage;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Control? _bodyContent;

    public MailTabViewModel(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailPollingService polling)
    {
        _config = config;
        _mail = mail;
        _polling = polling;
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
            Messages.Clear();
            foreach (var m in list) Messages.Add(m);
            Status = $"共 {Messages.Count} 封邮件";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedMessageChanged(MailMessage? value)
    {
        BodyContent = null;
        if (value == null) return;

        var body = value.HasHtml ? value.HtmlBody : null;
        BodyContent = HtmlToAvaloniaRenderer.Build(body, value.TextBody);
    }
}