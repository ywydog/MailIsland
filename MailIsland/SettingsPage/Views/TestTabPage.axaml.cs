using System.Drawing.Imaging;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Enums.SettingsWindow;
using ClassIsland.Core.Helpers.UI;
using ClassIsland.Core.Icons;
using ClassIsland.Core.Models.UI;
using ClassIsland.Shared;
using FluentAvalonia.UI.Controls;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.Services.Capture;

namespace MailIsland.SettingsPage.Views;

/// <summary>测试页：在调试分类下对各功能做快速自检。</summary>
[SettingsPageInfo("mailisland.debug", "MailIsland 测试", FluentIcons.BeakerRegular, FluentIcons.BeakerRegular,
    SettingsPageCategory.Debug)]
public partial class TestTabPage : SettingsPageBase
{
    private readonly MailIslandConfigHandler _config;
    private readonly ISmtpClientService _smtp;
    private readonly IMailClientService _mail;
    private readonly MailNotificationProvider _notificationProvider;
    private readonly MailPollingService _polling;

    private bool _busy;

    public TestTabPage()
    {
        InitializeComponent();

        _config = IAppHost.GetService<MailIslandConfigHandler>();
        _smtp = IAppHost.GetService<ISmtpClientService>();
        _mail = IAppHost.GetService<IMailClientService>();
        _notificationProvider = IAppHost.GetService<MailNotificationProvider>();
        _polling = IAppHost.GetService<MailPollingService>();

        StatusText.Text = "尚未执行测试。";
    }

    private async void OnTestSmtpClick(object? sender, RoutedEventArgs e)
    {
        await RunAsync("正在测试发信连接…", async (account, password, ct) =>
        {
            await _smtp.VerifyConnectionAsync(account, password, ct);
            return $"发信连接正常：{account.Email}";
        });
    }

    private async void OnTestImapClick(object? sender, RoutedEventArgs e)
    {
        await RunAsync("正在测试收信连接…", async (account, password, ct) =>
        {
            await _mail.VerifyConnectionAsync(account, password, ct);
            var mails = await _mail.FetchRecentAsync(account, password, 1, false, ct);
            return mails.Count > 0
                ? $"收信连接正常，最近一封：{mails[0].SubjectDisplay}"
                : "收信连接正常，收件箱暂无邮件。";
        });
    }

    private async void OnCaptureScreenClick(object? sender, RoutedEventArgs e)
    {
        await RunLocalAsync("正在获取屏幕截图…", () =>
        {
            using var bitmap = ScreenCapture.Capture();
            var path = BuildDesktopPath("screenshot", ".png");
            bitmap.Save(path, ImageFormat.Png);
            return Task.FromResult($"截图已保存到桌面：{Path.GetFileName(path)}（{bitmap.Width}×{bitmap.Height}）");
        });
    }

    private async void OnCaptureCameraClick(object? sender, RoutedEventArgs e)
    {
        await RunLocalAsync("正在获取摄像头画面…", async () =>
        {
            var photo = await CameraCapture.CapturePhotoAsync(null, CancellationToken.None);
            var path = BuildDesktopPath("camera", ".jpg");
            await File.WriteAllBytesAsync(path, photo.Bytes);
            return $"摄像头画面已保存到桌面：{Path.GetFileName(path)}（{photo.Width}×{photo.Height}）";
        });
    }

    private async void OnTestNotificationClick(object? sender, RoutedEventArgs e)
    {
        await RunAsync("正在推送示例提醒…", (account, _, _) =>
        {
            _notificationProvider.Notify(
                new MailMessage
                {
                    SenderName = "MailIsland",
                    SenderEmail = account.Email,
                    Subject = "这是一条测试提醒",
                    Date = DateTimeOffset.Now,
                    TextBody = "来自 MailIsland 测试页的示例提醒，用于检查提醒显示效果。",
                },
                account,
                _config.Data,
                keywordHit: false);

            return Task.FromResult(_config.Data.NotifyEnabled
                ? "已推送示例提醒。"
                : "已推送示例提醒，但“启用提醒”当前是关闭状态。");
        });
    }

    private async void OnTestSyncClick(object? sender, RoutedEventArgs e)
    {
        if (_polling.IsSyncing)
        {
            Report("正在同步中，请稍后再试。", FAInfoBarSeverity.Warning);
            return;
        }

        await RunAsync("正在同步收件箱…", async (_, _, ct) =>
        {
            await _polling.SyncOnceAsync(ct);
            return _polling.LastSyncTime is { } time
                ? $"同步完成，上次同步时间：{time.ToLocalTime():HH:mm:ss}"
                : "同步完成。";
        });
    }

    /// <summary>统一处理账号解析、忙碌状态与结果提示。</summary>
    private async Task RunAsync(
        string startedMessage,
        Func<MailAccountSettings, string, CancellationToken, Task<string>> action)
    {
        if (_busy) return;

        var account = ResolveAccount();
        if (account == null)
        {
            Report("没有可用的邮箱账号，请先在“账号”页添加并填写授权码。", FAInfoBarSeverity.Warning);
            return;
        }

        _busy = true;
        StatusText.Text = startedMessage;
        try
        {
            var password = _config.Protector.Decrypt(account.EncryptedPassword);
            var message = await action(account, password, CancellationToken.None);
            Report(message, FAInfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Report($"操作失败：{ex.Message}", FAInfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    /// <summary>执行不需要邮箱账号的本地测试。</summary>
    private async Task RunLocalAsync(string startedMessage, Func<Task<string>> action)
    {
        if (_busy) return;

        _busy = true;
        StatusText.Text = startedMessage;
        try
        {
            Report(await action(), FAInfoBarSeverity.Success);
        }
        catch (Exception ex)
        {
            Report($"操作失败：{ex.Message}", FAInfoBarSeverity.Error);
        }
        finally
        {
            _busy = false;
        }
    }

    private static string BuildDesktopPath(string prefix, string extension)
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
        {
            throw new InvalidOperationException("找不到桌面目录。");
        }

        return Path.Combine(desktop, $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss-fff}{extension}");
    }

    private MailAccountSettings? ResolveAccount() => _config.Data.Accounts
        .FirstOrDefault(account => account.Enabled &&
                                   !string.IsNullOrWhiteSpace(account.Email) &&
                                   !string.IsNullOrWhiteSpace(account.EncryptedPassword));

    private void Report(string message, FAInfoBarSeverity severity)
    {
        StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
        this.ShowToast(new ToastMessage(message)
        {
            Severity = severity,
            Duration = TimeSpan.FromSeconds(8),
        });
    }
}
