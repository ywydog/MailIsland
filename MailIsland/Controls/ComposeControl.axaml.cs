using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.SettingsPage.SettingsViewModels;

namespace MailIsland.Controls;

/// <summary>可复用的写邮件界面，由邮件页与发件箱页共同使用。</summary>
public partial class ComposeControl : UserControl
{
    private ComposeViewModel? _compose;

    /// <summary>关闭（取消或发送完成）时触发。</summary>
    public event EventHandler? Closed;

    /// <summary>发送成功时触发。</summary>
    public event EventHandler? Sent;

    public ComposeControl()
    {
        InitializeComponent();
    }

    /// <summary>开始一封新邮件；<paramref name="quote"/> 为引用原文。</summary>
    public void Begin(MailAccountSettings account, string? to = null, string? subject = null, string? quote = null)
    {
        _compose = new ComposeViewModel(
            account,
            IAppHost.GetService<MailIslandConfigHandler>(),
            IAppHost.GetService<ISmtpClientService>(),
            IAppHost.GetService<IMailClientService>());
        _compose.ApplyPrefill(to, subject, quote);

        DataContext = _compose;
        DiscardOverlay.IsVisible = false;
        IsVisible = true;
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        if (_compose?.HasContent == true)
        {
            DiscardOverlay.IsVisible = true;
            return;
        }

        Close();
    }

    private void OnDiscardCancelClick(object? sender, RoutedEventArgs e) =>
        DiscardOverlay.IsVisible = false;

    private void OnDiscardConfirmClick(object? sender, RoutedEventArgs e)
    {
        DiscardOverlay.IsVisible = false;
        Close();
    }

    private async void OnSendClick(object? sender, RoutedEventArgs e)
    {
        if (_compose is null) return;

        if (await _compose.SendAsync())
        {
            Sent?.Invoke(this, EventArgs.Empty);
            Close();
        }
    }

    private async void OnAddAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (_compose is null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择附件",
            AllowMultiple = true,
        });

        _compose.AddAttachments(files.Select(file => file.Path.LocalPath));
    }

    private void OnRemoveAttachmentClick(object? sender, RoutedEventArgs e)
    {
        if (_compose is not null && sender is Button { DataContext: ComposeAttachment attachment })
        {
            _compose.RemoveAttachment(attachment);
        }
    }

    private void Close()
    {
        IsVisible = false;
        DataContext = null;
        _compose = null;

        Closed?.Invoke(this, EventArgs.Empty);
    }
}
