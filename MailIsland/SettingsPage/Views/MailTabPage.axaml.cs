using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.Shared;
using MailIsland.SettingsPage.SettingsViewModels;
using MailIsland.Storage;

namespace MailIsland.SettingsPage.Views;

/// <summary>邮件设置页。</summary>
[Group(GlobalConstants.SettingsGroupId)]
[SettingsPageInfo("mailisland.settings.mail", "邮件", "\uE8B7", "\uE8B7")]
public partial class MailTabPage : SettingsPageBase
{
    private readonly MailTabViewModel _vm;

    public MailTabPage()
    {
        InitializeComponent();

        _vm = new MailTabViewModel(
            IAppHost.GetService<MailIslandConfigHandler>(),
            IAppHost.GetService<IMailClientService>(),
            IAppHost.GetService<MailCacheStore>());
        DataContext = _vm;

        AccountCombo.ItemsSource = _vm.Accounts;
        AccountCombo.SelectionChanged += OnAccountComboChanged;
        RefreshButton.Click += OnRefreshClick;
        MessageList.SelectionChanged += OnMessageSelectionChanged;
        ComposeView.Closed += (_, _) =>
        {
            MessageList.SelectedItem = null;
            ShowList();
        };

        if ((_vm.Accounts.FirstOrDefault(a => a.IsSelected) ?? _vm.Accounts.FirstOrDefault()) is { } first)
        {
            AccountCombo.SelectedItem = first;
            _vm.SelectedAccount = first;
        }
    }

    private void OnAccountComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AccountCombo.SelectedItem is Logic.Config.MailAccountSettings acc)
        {
            _vm.SelectedAccount = acc;
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        await _vm.LoadAsync();
    }

    private void OnMessageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MessageList.SelectedItem is not MailMessage msg)
        {
            ShowList();
            return;
        }

        _vm.SelectedMessage = msg;

        DetailSubject.Text = msg.SubjectDisplay;

        var senderName = msg.SenderName.Trim().Trim('"').Trim();
        var from = string.IsNullOrWhiteSpace(msg.SenderEmail)
            ? msg.SenderDisplay
            : string.IsNullOrWhiteSpace(senderName)
                ? msg.SenderEmail
                : $"{senderName} <{msg.SenderEmail}>";
        DetailFrom.Text = $"发件人：{from}";
        DetailDate.Text = msg.Date == default
            ? "时间：未知"
            : $"时间：{msg.Date.ToLocalTime():yyyy-MM-dd HH:mm}";

        BodyHost.Content = _vm.BodyContent;
        AttachmentHeader.IsVisible = _vm.Attachments.Count > 0;

        DetailContent.IsVisible = true;
        DetailHint.IsVisible = false;

        ShowDetailPage();
    }

    /// <summary>切换到整页详情。</summary>
    private void ShowDetailPage()
    {
        ListView.IsVisible = false;
        HeaderBar.IsVisible = false;
        DetailView.IsVisible = true;
    }

    /// <summary>返回整页列表，并清空详情内容。</summary>
    private void ShowList()
    {
        _vm.SelectedMessage = null;

        DetailSubject.Text = "";
        DetailFrom.Text = "";
        DetailDate.Text = "";
        BodyHost.Content = null;
        AttachmentHeader.IsVisible = false;
        DetailContent.IsVisible = false;
        DetailHint.IsVisible = true;

        DetailView.IsVisible = false;
        ListView.IsVisible = true;
        HeaderBar.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        // 清空选择，保证再次点击同一封邮件仍能打开详情
        MessageList.SelectedItem = null;
        ShowList();

        // “仅看未读”下，已读邮件在返回列表后立即移除
        _vm.RefreshFilter();
    }

    private async void OnDownloadAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MailAttachmentInfo attachment })
        {
            await _vm.DownloadAttachmentAsync(attachment);
        }
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) => _vm.OpenContainingFolder();

    private void OnLoadImagesOnceClick(object? sender, RoutedEventArgs e) => _vm.LoadImagesOnce();

    // ---------- 写邮件 / 回复 / 转发（复用发件箱页的写邮件控件） ----------

    private void OnComposeClick(object? sender, RoutedEventArgs e) => OpenCompose(null, null, null);

    private void OnReplyClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMessage is not { } mail) return;

        var quote = _vm.SelectedAccount?.QuoteOriginal == true ? BuildQuote(mail) : null;
        OpenCompose(mail.SenderEmail, PrefixSubject(mail.Subject, "Re:"), quote);
    }

    private void OnForwardClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedMessage is not { } mail) return;

        OpenCompose(null, PrefixSubject(mail.Subject, "Fwd:"), BuildQuote(mail));
    }

    private void OpenCompose(string? to, string? subject, string? quote)
    {
        if (_vm.SelectedAccount is not { } account)
        {
            _vm.Status = "请先到“账号”页添加邮箱账号。";
            return;
        }

        HeaderBar.IsVisible = false;
        ListView.IsVisible = false;
        DetailView.IsVisible = false;
        ComposeView.Begin(account, to, subject, quote);
    }

    private static string PrefixSubject(string subject, string prefix)
    {
        var text = string.IsNullOrWhiteSpace(subject) ? "(无主题)" : subject.Trim();
        return text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{prefix} {text}";
    }

    private static string BuildQuote(MailMessage mail)
    {
        var senderName = mail.SenderName.Trim().Trim('"').Trim();
        var sender = string.IsNullOrWhiteSpace(mail.SenderEmail)
            ? senderName
            : string.IsNullOrWhiteSpace(senderName)
                ? mail.SenderEmail
                : $"{senderName} <{mail.SenderEmail}>";
        var date = mail.Date == default
            ? "未知"
            : mail.Date.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        return $"\n\n---------- 原邮件 ----------\n发件人：{sender}\n时间：{date}\n\n{mail.TextBody}";
    }
}