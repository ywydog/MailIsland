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

        if (_vm.Accounts.FirstOrDefault() is { } first)
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
            ShowEmptyDetail();
            return;
        }

        _vm.SelectedMessage = msg;

        DetailSubject.Text = msg.SubjectDisplay;

        var from = string.IsNullOrWhiteSpace(msg.SenderEmail)
            ? msg.SenderDisplay
            : string.IsNullOrWhiteSpace(msg.SenderName)
                ? msg.SenderEmail
                : $"{msg.SenderName} <{msg.SenderEmail}>";
        DetailFrom.Text = $"发件人：{from}";
        DetailDate.Text = msg.Date == default
            ? "时间：未知"
            : $"时间：{msg.Date.ToLocalTime():yyyy-MM-dd HH:mm}";

        BodyHost.Content = _vm.BodyContent;
        AttachmentHeader.IsVisible = _vm.Attachments.Count > 0;

        DetailContent.IsVisible = true;
        DetailHint.IsVisible = false;
    }

    private void ShowEmptyDetail()
    {
        _vm.SelectedMessage = null;

        DetailSubject.Text = "";
        DetailFrom.Text = "";
        DetailDate.Text = "";
        BodyHost.Content = null;
        AttachmentHeader.IsVisible = false;

        DetailContent.IsVisible = false;
        DetailHint.IsVisible = true;
    }

    private async void OnDownloadAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MailAttachmentInfo attachment })
        {
            await _vm.DownloadAttachmentAsync(attachment);
        }
    }

    private void OnOpenFolderClick(object? sender, RoutedEventArgs e) => _vm.OpenContainingFolder();
}