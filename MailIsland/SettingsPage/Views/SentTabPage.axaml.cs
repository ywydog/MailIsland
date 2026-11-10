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

namespace MailIsland.SettingsPage.Views;

/// <summary>发件箱设置页：浏览已发送邮件并写新邮件。</summary>
[Group(GlobalConstants.SettingsGroupId)]
[SettingsPageInfo("mailisland.settings.sent", "发件箱", "\uE724", "\uE724")]
public partial class SentTabPage : SettingsPageBase
{
    private readonly SentTabViewModel _vm;

    public SentTabPage()
    {
        InitializeComponent();

        _vm = new SentTabViewModel(
            IAppHost.GetService<MailIslandConfigHandler>(),
            IAppHost.GetService<IMailClientService>());
        DataContext = _vm;

        AccountCombo.ItemsSource = _vm.Accounts;
        AccountCombo.SelectionChanged += OnAccountComboChanged;
        MessageList.SelectionChanged += OnMessageSelectionChanged;

        ComposeView.Closed += OnComposeClosed;

        if (_vm.Accounts.FirstOrDefault() is { } first)
        {
            AccountCombo.SelectedItem = first;
            _vm.SelectedAccount = first;
        }
    }

    private void OnAccountComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AccountCombo.SelectedItem is Logic.Config.MailAccountSettings account)
        {
            _vm.SelectedAccount = account;
        }
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs e) => await _vm.LoadAsync();

    private void OnMessageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MessageList.SelectedItem is not MailMessage mail)
        {
            ShowList();
            return;
        }

        _vm.SelectedMessage = mail;

        DetailSubject.Text = mail.SubjectDisplay;
        DetailTo.Text = $"收件人：{mail.RecipientsDisplay}";
        DetailDate.Text = mail.Date == default
            ? "时间：未知"
            : $"时间：{mail.Date.ToLocalTime():yyyy-MM-dd HH:mm}";

        BodyHost.Content = _vm.BodyContent;
        AttachmentHeader.IsVisible = _vm.Attachments.Count > 0;

        HeaderBar.IsVisible = false;
        ListView.IsVisible = false;
        DetailView.IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        MessageList.SelectedItem = null;
        ShowList();
    }

    private void OnComposeClick(object? sender, RoutedEventArgs e)
    {
        if (_vm.SelectedAccount is not { } account)
        {
            _vm.Status = "请先到“账号”页添加邮箱账号。";
            return;
        }

        HeaderBar.IsVisible = false;
        ListView.IsVisible = false;
        DetailView.IsVisible = false;

        ComposeView.Begin(account);
    }

    private void OnComposeClosed(object? sender, EventArgs e)
    {
        ShowList();
        _ = _vm.LoadAsync();
    }

    private async void OnDownloadAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MailAttachmentInfo attachment })
        {
            await _vm.DownloadAttachmentAsync(attachment);
        }
    }

    private void ShowList()
    {
        _vm.SelectedMessage = null;

        DetailSubject.Text = "";
        DetailTo.Text = "";
        DetailDate.Text = "";
        BodyHost.Content = null;
        AttachmentHeader.IsVisible = false;

        DetailView.IsVisible = false;
        HeaderBar.IsVisible = true;
        ListView.IsVisible = true;
    }
}
