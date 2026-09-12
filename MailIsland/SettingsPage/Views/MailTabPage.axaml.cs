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
using MailIsland.SettingsPage.SettingsViewModels;

namespace MailIsland.SettingsPage.Views;

/// <summary>邮件设置页。</summary>
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
            IAppHost.GetService<MailPollingService>());
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
        StatusText.Text = _vm.Status;
    }

    private void OnMessageSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (MessageList.SelectedItem is MailMessage msg)
        {
            _vm.SelectedMessage = msg;
            DetailSubject.Text = msg.Subject;
            DetailFrom.Text = $"发件人：{msg.SenderName} <{msg.SenderEmail}>";
            DetailDate.Text = $"时间：{msg.Date:yyyy-MM-dd HH:mm}";
            BodyHost.Content = _vm.BodyContent;
        }
    }
}