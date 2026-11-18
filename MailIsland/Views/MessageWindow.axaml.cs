using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassIsland.Core.Controls;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Models;
using MailIsland.Services;
using MailIsland.Storage;
using MailIsland.ViewModels;
using Microsoft.Extensions.Logging;

namespace MailIsland.Views;

/// <summary>
/// 新邮件消息窗口。居中置顶、不可关闭和最小化，正文可自动滚动并在滚动结束后开始倒计时。
/// </summary>
public partial class MessageWindow : MyWindow
{
    /// <summary>自动滚动速度。正文很长时保持可读，同时避免窗口停留过久。</summary>
    private const double ScrollPixelsPerSecond = 40;

    private readonly MessageWindowViewModel _viewModel = new();

    private MessageWindowRequest? _request;
    private MailIslandConfigHandler? _config;
    private IMailClientService? _mailService;
    private ILogger<MessageWindow>? _logger;

    private bool _allowClose;
    private bool _isClosing;
    private bool _isBusy;
    private bool _presentationStarted;
    private int _layoutAttempts;

    private DispatcherTimer? _scrollTimer;
    private DispatcherTimer? _countdownTimer;
    private CancellationTokenSource? _downloadCts;
    private int _countdownRemaining;

    public MessageWindow()
    {
        InitializeComponent();
        DataContext = _viewModel;

        // 只能通过“收到”、倒计时结束或应用退出关闭
        Closing += (_, e) =>
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
        };

        // 即使宿主或系统尝试最小化，也立即还原
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty && WindowState == WindowState.Minimized)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    WindowState = WindowState.Normal;
                    Activate();
                }, DispatcherPriority.MaxValue);
            }
        };

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    public MessageWindow(
        MessageWindowRequest request,
        MailIslandConfigHandler config,
        IMailClientService mailService,
        ILogger<MessageWindow> logger) : this()
    {
        _request = request;
        _config = config;
        _mailService = mailService;
        _logger = logger;

        ApplyConfiguredSize(request.Settings);
        PrepareContent();
    }

    private bool IsPreview => _request?.IsPreview == true;

    /// <summary>应用默认窗口大小，并限制在屏幕工作区内。</summary>
    private void ApplyConfiguredSize(MessageWindowSettings settings)
    {
        var width = Math.Clamp(
            settings.WindowWidth,
            MessageWindowSettings.MinWindowWidth,
            MessageWindowSettings.MaxWindowWidth);
        var height = Math.Clamp(
            settings.WindowHeight,
            MessageWindowSettings.MinWindowHeight,
            MessageWindowSettings.MaxWindowHeight);

        if (Screens.Primary is { } screen)
        {
            var scaling = screen.Scaling <= 0 ? 1 : screen.Scaling;
            var maxWidth = screen.WorkingArea.Width / scaling * 0.9;
            var maxHeight = screen.WorkingArea.Height / scaling * 0.9;
            width = Math.Min(width, (int)Math.Max(MessageWindowSettings.MinWindowWidth, maxWidth));
            height = Math.Min(height, (int)Math.Max(MessageWindowSettings.MinWindowHeight, maxHeight));
        }

        Width = width;
        Height = height;
    }

    private void PrepareContent()
    {
        if (_request is null)
        {
            return;
        }

        var mail = _request.Mail;
        var type = _request.TypeRule;

        _viewModel.Sender = mail.SenderDisplay;
        _viewModel.MessageType = type?.Name ?? "";
        _viewModel.HasMessageType = !string.IsNullOrWhiteSpace(type?.Name);
        _viewModel.Body = string.IsNullOrWhiteSpace(mail.TextBody) ? "(无正文)" : mail.TextBody;
        _viewModel.AttachmentSummary = mail.Attachments.Count > 0
            ? $"含 {mail.Attachments.Count} 个附件"
            : "";
        _viewModel.AcknowledgeText = IsPreview ? "看完了" : _request.Settings.AcknowledgeText;
    }

    private void OnLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (IsPreview)
        {
            _viewModel.IsStatusVisible = false;
            return;
        }

        Dispatcher.UIThread.Post(TryStartPresentation, DispatcherPriority.Background);
    }

    /// <summary>等待布局完成后判断正文是否需要滚动，再决定进入滚动还是倒计时。</summary>
    private void TryStartPresentation()
    {
        if (_isClosing || IsPreview || _presentationStarted)
        {
            return;
        }

        if (BodyScroll.Viewport.Height <= 0 && _layoutAttempts++ < 5)
        {
            Dispatcher.UIThread.Post(TryStartPresentation, DispatcherPriority.Background);
            return;
        }

        _presentationStarted = true;
        var hasOverflow = BodyScroll.Extent.Height > BodyScroll.Viewport.Height + 1;
        if (hasOverflow && (_request?.Settings.AutoScroll ?? false))
        {
            BeginAutoScroll();
            return;
        }

        StartCountdown();
    }

    private void BeginAutoScroll()
    {
        _viewModel.IsStatusVisible = true;
        _viewModel.IsProgressIndeterminate = true;
        _viewModel.StatusText = "正文滚动中......";

        _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _scrollTimer.Tick += OnScrollTick;
        _scrollTimer.Start();
    }

    private void OnScrollTick(object? sender, EventArgs e)
    {
        var maxOffset = GetMaxScrollOffset();
        if (maxOffset <= 0)
        {
            StopAutoScroll();
            StartCountdown();
            return;
        }

        var next = Math.Min(
            maxOffset,
            BodyScroll.Offset.Y + ScrollPixelsPerSecond * 0.03);
        BodyScroll.Offset = new Vector(0, next);

        if (next >= maxOffset - 0.5)
        {
            StopAutoScroll();
            StartCountdown();
        }
    }

    private double GetMaxScrollOffset() =>
        Math.Max(0, BodyScroll.Extent.Height - BodyScroll.Viewport.Height);

    /// <summary>窗口缩放时重新判断正文是否仍需要滚动。</summary>
    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_isClosing || IsPreview || _scrollTimer is null)
        {
            return;
        }

        var maxOffset = GetMaxScrollOffset();
        if (maxOffset <= 0 || BodyScroll.Offset.Y >= maxOffset - 0.5)
        {
            StopAutoScroll();
            StartCountdown();
            return;
        }

        if (BodyScroll.Offset.Y > maxOffset)
        {
            BodyScroll.Offset = new Vector(0, maxOffset);
        }
    }

    private void StopAutoScroll()
    {
        if (_scrollTimer is null)
        {
            return;
        }

        _scrollTimer.Stop();
        _scrollTimer.Tick -= OnScrollTick;
        _scrollTimer = null;
    }

    private void StartCountdown()
    {
        if (IsPreview || _countdownTimer is not null)
        {
            return;
        }

        StopAutoScroll();
        var seconds = Math.Clamp(
            _request?.Settings.DisplaySeconds ?? MessageWindowSettings.DefaultDisplaySeconds,
            MessageWindowSettings.MinDisplaySeconds,
            MessageWindowSettings.MaxDisplaySeconds);

        _countdownRemaining = seconds;
        _viewModel.IsStatusVisible = true;
        _viewModel.IsProgressIndeterminate = false;
        _viewModel.ProgressMaximum = seconds;
        _viewModel.ProgressValue = seconds;
        _viewModel.StatusText = FormatCountdown(seconds);

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += OnCountdownTick;
        _countdownTimer.Start();
    }

    private void OnCountdownTick(object? sender, EventArgs e)
    {
        _countdownRemaining--;
        if (_countdownRemaining > 0)
        {
            _viewModel.ProgressValue = _countdownRemaining;
            _viewModel.StatusText = FormatCountdown(_countdownRemaining);
            return;
        }

        StopCountdown();
        CloseProgrammatically();
    }

    private void StopCountdown()
    {
        if (_countdownTimer is not null)
        {
            _countdownTimer.Stop();
            _countdownTimer.Tick -= OnCountdownTick;
            _countdownTimer = null;
        }

        _countdownRemaining = 0;
        _viewModel.IsStatusVisible = false;
        _viewModel.IsProgressIndeterminate = false;
        _viewModel.ProgressValue = 0;
    }

    /// <summary>沿用询问窗的倒计时文案格式。</summary>
    private static string FormatCountdown(int seconds)
    {
        var minutes = seconds / 60;
        var remainingSeconds = seconds % 60;
        return $"将{minutes}分{remainingSeconds}秒后关闭......";
    }

    private async void OnAcknowledgeClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isBusy || _isClosing)
        {
            return;
        }

        StopAutoScroll();
        StopCountdown();

        if (IsPreview || _request is null || !_request.Settings.DownloadAttachmentsOnAcknowledge ||
            _request.Mail.Attachments.Count == 0 || _request.Account is null)
        {
            CloseProgrammatically();
            return;
        }

        await DownloadAttachmentsAsync();
    }

    private async Task DownloadAttachmentsAsync()
    {
        if (_request?.Account is not { } account || _config is null || _mailService is null)
        {
            CloseProgrammatically();
            return;
        }

        _isBusy = true;
        _downloadCts = new CancellationTokenSource();
        _viewModel.IsAcknowledgeEnabled = false;
        _viewModel.IsForceCloseVisible = false;
        _viewModel.IsStatusVisible = true;
        _viewModel.IsProgressIndeterminate = true;
        _viewModel.StatusText = "正在下载附件......";

        var downloaded = 0;
        try
        {
            if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
            {
                throw new InvalidOperationException("账号未保存授权码");
            }

            var password = _config.Protector.Decrypt(account.EncryptedPassword);
            var directory = AttachmentStorage.GetDownloadDirectory();
            var attachments = _request.Mail.Attachments;

            for (var i = 0; i < attachments.Count; i++)
            {
                _viewModel.StatusText = $"正在下载附件 ({i + 1}/{attachments.Count})......";
                var attachment = attachments[i];
                var content = await _mailService.DownloadAttachmentAsync(
                    account,
                    password,
                    _request.Mail.Uid,
                    attachment.PartSpecifier,
                    _downloadCts.Token);

                var path = AttachmentStorage.BuildTargetPath(directory, attachment.FileName);
                await File.WriteAllBytesAsync(path, content, _downloadCts.Token);
                downloaded++;
            }

            CloseProgrammatically();
        }
        catch (OperationCanceledException) when (_isClosing)
        {
            // 窗口被应用退出或“仍然关闭”关闭，无需提示
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "下载消息窗口附件失败");
            _viewModel.StatusText = downloaded > 0
                ? $"已保存 {downloaded} 个附件，其余附件下载失败，请重试。"
                : "附件下载失败，请检查网络或账号授权码后重试。";
            _viewModel.IsProgressIndeterminate = false;
            _viewModel.ProgressMaximum = 1;
            _viewModel.ProgressValue = 0;
            _viewModel.IsAcknowledgeEnabled = true;
            _viewModel.AcknowledgeText = "重试";
            _viewModel.IsForceCloseVisible = true;
            _isBusy = false;
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void OnForceCloseClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseProgrammatically();
    }

    /// <summary>应用退出时由服务调用，绕过关闭拦截。</summary>
    public void ForceClose() => CloseProgrammatically();

    private void CloseProgrammatically()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _allowClose = true;
        _downloadCts?.Cancel();
        StopAutoScroll();
        StopCountdown();
        Close();
    }
}
