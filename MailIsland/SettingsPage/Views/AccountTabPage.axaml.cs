using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Presets;
using MailIsland.Services;
using MailIsland.Shared;
using MailIsland.SettingsPage.SettingsViewModels;
using MailIsland.Storage;

namespace MailIsland.SettingsPage.Views;

/// <summary>账号设置页。</summary>
[Group(GlobalConstants.SettingsGroupId)]
[SettingsPageInfo("mailisland.settings.account", "账号", "\uE078", "\uE078")]
public partial class AccountTabPage : SettingsPageBase
{
    private readonly AccountTabViewModel _vm;

    public AccountTabPage()
    {
        InitializeComponent();

        _vm = new AccountTabViewModel(
            IAppHost.GetService<MailIslandConfigHandler>(),
            IAppHost.GetService<IMailClientService>(),
            IAppHost.GetService<MailCacheStore>());
        DataContext = _vm;

        SetupControls();
        RefreshList(clearPassword: true);
    }

    private void SetupControls()
    {
        AddButton.Click += OnAddClick;
        DeleteButton.Click += OnDeleteClick;
        TestButton.Click += OnTestClick;
        SaveButton.Click += OnSaveClick;

        PresetCombo.ItemsSource = _vm.Presets;
        PresetCombo.SelectionChanged += OnPresetChanged;

        AccountList.ItemsSource = _vm.Accounts;
        AccountList.SelectionChanged += OnAccountSelectionChanged;

        // 绑定字段到当前账号
        DisplayNameBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && _vm.Current != null) _vm.Current.DisplayName = DisplayNameBox.Text ?? "";
        };
        EmailBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && _vm.Current != null) _vm.Current.Email = EmailBox.Text ?? "";
        };
        ImapServerBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && _vm.Current != null) _vm.Current.ImapServer = ImapServerBox.Text ?? "";
        };
        SmtpServerBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(TextBox.Text) && _vm.Current != null) _vm.Current.SmtpServer = SmtpServerBox.Text ?? "";
        };
        ImapPortBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(TextBox.Text) || _vm.Current == null) return;
            if (int.TryParse(ImapPortBox.Text, out var p)) _vm.Current.ImapPort = p;
        };
        SmtpPortBox.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name != nameof(TextBox.Text) || _vm.Current == null) return;
            if (int.TryParse(SmtpPortBox.Text, out var p)) _vm.Current.SmtpPort = p;
        };
        ImapSslCheck.IsCheckedChanged += (_, _) =>
        {
            if (_vm.Current != null) _vm.Current.ImapUseSsl = ImapSslCheck.IsChecked ?? true;
        };
        SmtpSslCheck.IsCheckedChanged += (_, _) =>
        {
            if (_vm.Current != null) _vm.Current.SmtpUseSsl = SmtpSslCheck.IsChecked ?? true;
        };

        NotifyToggle.IsCheckedChanged += (_, _) =>
        {
            if (_vm.Current != null) _vm.Current.NotifyEnabled = NotifyToggle.IsChecked ?? true;
        };

        QuoteToggle.IsCheckedChanged += (_, _) =>
        {
            if (_vm.Current != null) _vm.Current.QuoteOriginal = QuoteToggle.IsChecked ?? true;
        };

        SignatureBox.TextChanged += (_, _) =>
        {
            if (_vm.Current != null) _vm.Current.Signature = SignatureBox.Text ?? "";
        };
    }

    private void RefreshList(bool clearPassword = false)
    {
        EditorArea.IsVisible = _vm.Current != null;
        if (_vm.Current is not { } acc) return;

        DisplayNameBox.Text = acc.DisplayName;
        EmailBox.Text = acc.Email;
        ImapServerBox.Text = acc.ImapServer;
        ImapPortBox.Text = acc.ImapPort.ToString();
        SmtpServerBox.Text = acc.SmtpServer;
        SmtpPortBox.Text = acc.SmtpPort.ToString();
        ImapSslCheck.IsChecked = acc.ImapUseSsl;
        SmtpSslCheck.IsChecked = acc.SmtpUseSsl;
        NotifyToggle.IsChecked = acc.NotifyEnabled;
        QuoteToggle.IsChecked = acc.QuoteOriginal;
        SignatureBox.Text = acc.Signature;

        // 切换/新增/删除账号后必须清空授权码，避免把上一个账号的授权码写入当前账号
        if (clearPassword)
        {
            PasswordBox.Text = "";
            _vm.PasswordPlain = "";
        }

        StatusText.Text = _vm.Status;
        StatusText.IsVisible = !string.IsNullOrEmpty(_vm.Status);
    }

    private void OnAccountSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AccountList.SelectedItem is Logic.Config.MailAccountSettings acc)
        {
            foreach (var a in _vm.Accounts) a.IsSelected = false;
            acc.IsSelected = true;
            _vm.Current = acc;
            RefreshList(clearPassword: true);
        }
    }

    private void OnAddClick(object? sender, RoutedEventArgs e)
    {
        _vm.AddAccount();
        RefreshList(clearPassword: true);
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        _vm.DeleteAccount();
        RefreshList(clearPassword: true);
    }

    private void OnPresetChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is MailPreset preset)
        {
            _vm.ApplyPreset(preset);
            RefreshList();
        }
    }

    private async void OnTestClick(object? sender, RoutedEventArgs e)
    {
        _vm.PasswordPlain = PasswordBox.Text ?? "";
        await _vm.TestConnectionAsync();
        StatusText.Text = _vm.Status;
        StatusText.IsVisible = true;
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        _vm.PasswordPlain = PasswordBox.Text ?? "";
        _vm.ApplyPasswordAndSave();
        _vm.SaveAll();
        PasswordBox.Text = "";
        StatusText.Text = _vm.Status;
        StatusText.IsVisible = true;
        RefreshList(clearPassword: true);
    }
}