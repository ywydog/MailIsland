using Avalonia.Threading;
using ClassIsland.Core.Abstractions.Services.SpeechService;
using ClassIsland.Shared;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Messages;
using MailIsland.Models;
using MailIsland.Views;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services;

/// <summary>
/// 消息窗口队列。同一时间只显示一个窗口，关闭后自动显示下一封。
/// </summary>
public sealed class MessageWindowService
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<MessageWindowService> _logger;

    private readonly Queue<MessageWindowRequest> _queue = new();
    private readonly List<MessageWindow> _previewWindows = new();
    private MessageWindow? _current;
    private bool _isShuttingDown;

    public MessageWindowService(
        MailIslandConfigHandler config,
        IMailClientService mail,
        ILoggerFactory loggerFactory,
        ILogger<MessageWindowService> logger)
    {
        _config = config;
        _mail = mail;
        _loggerFactory = loggerFactory;
        _logger = logger;
    }

    /// <summary>把一封新邮件加入消息窗口队列。</summary>
    public void Enqueue(MailMessage mail, MailAccountSettings account, MailIslandConfigData config)
    {
        if (!config.MessageWindow.Enabled)
        {
            return;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => Enqueue(mail, account, config));
            return;
        }

        if (_isShuttingDown)
        {
            return;
        }

        var settings = config.MessageWindow.Clone();
        settings.Normalize();
        var type = MessageTypeMatcher.Match(mail.Subject, settings.TypeRules);

        _queue.Enqueue(new MessageWindowRequest
        {
            Mail = mail,
            Account = account,
            Settings = settings,
            TypeRule = type,
        });

        ShowNext();
    }

    /// <summary>在设置页按当前配置打开预览窗口，不进入正式队列。</summary>
    public void ShowPreview(MessageWindowSettings settings)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => ShowPreview(settings));
            return;
        }

        if (_isShuttingDown)
        {
            return;
        }

        var snapshot = settings.Clone();
        snapshot.Normalize();

        var sample = new MailMessage
        {
            SenderName = "张老师",
            SenderEmail = "teacher@example.com",
            Subject = "【作业通知】第三章练习",
            TextBody = string.Join(
                "\n\n",
                Enumerable.Repeat(
                    "这是一封用于预览消息窗口的示例邮件。正文较长时会自动向下滚动，滚动结束后开始倒计时。",
                    12)),
        };

        var request = new MessageWindowRequest
        {
            Mail = sample,
            Account = null,
            Settings = snapshot,
            TypeRule = MessageTypeMatcher.Match(sample.Subject, snapshot.TypeRules),
            IsPreview = true,
        };

        var window = CreateWindow(request);
        _previewWindows.Add(window);
        window.Closed += (_, _) => _previewWindows.Remove(window);
        window.Show();
        window.Activate();
    }

    /// <summary>应用退出时清空队列并关闭当前窗口。</summary>
    public void Shutdown()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Shutdown);
            return;
        }

        _isShuttingDown = true;
        _queue.Clear();
        _current?.ForceClose();
        _current = null;

        foreach (var window in _previewWindows.ToList())
        {
            window.ForceClose();
        }

        _previewWindows.Clear();
    }

    private void ShowNext()
    {
        if (_isShuttingDown || _current is not null || _queue.Count == 0)
        {
            return;
        }

        var request = _queue.Dequeue();
        try
        {
            var window = CreateWindow(request);
            _current = window;
            window.Closed += OnWindowClosed;
            window.Show();
            window.Activate();
            EnqueueSpeech(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "显示消息窗口失败");
            _current = null;
            Dispatcher.UIThread.Post(ShowNext);
        }
    }

    private MessageWindow CreateWindow(MessageWindowRequest request) =>
        new(request, _config, _mail, _loggerFactory.CreateLogger<MessageWindow>());

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is MessageWindow window)
        {
            window.Closed -= OnWindowClosed;
        }

        _current = null;
        Dispatcher.UIThread.Post(ShowNext);
    }

    private void EnqueueSpeech(MessageWindowRequest request)
    {
        if (!request.Settings.SpeechEnabled || request.IsPreview)
        {
            return;
        }

        var speech = MessageSpeechBuilder.Build(
            request.Mail.SenderDisplay,
            request.Mail.Subject,
            request.TypeRule,
            request.Settings.DefaultSpeechText);

        if (string.IsNullOrWhiteSpace(speech))
        {
            return;
        }

        try
        {
            IAppHost.TryGetService<ISpeechService>()?.EnqueueSpeechQueue(speech);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "消息窗口语音播报入队失败");
        }
    }
}
