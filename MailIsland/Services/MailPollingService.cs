using Avalonia.Threading;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Keyword;
using MailIsland.Logic.Mail;
using MailIsland.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MailIsland.Services;

/// <summary>
/// 后台轮询服务：按配置间隔拉取各启用账号的新邮件，
/// 命中关键词（或无关键词规则时）推送提醒，并触发同步状态事件。
/// </summary>
public sealed class MailPollingService : BackgroundService
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly MailNotificationProvider _notifier;
    private readonly ILogger<MailPollingService> _logger;

    private readonly Dictionary<string, uint> _lastUidByAccount = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);

    public MailPollingService(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailNotificationProvider notifier,
        ILogger<MailPollingService> logger)
    {
        _config = config;
        _mail = mail;
        _notifier = notifier;
        _logger = logger;
    }

    /// <summary>同步状态变化：arg = 描述文本。状态供 UI 展示，统一切到 UI 线程通知。</summary>
    public event EventHandler<string>? SyncStatusChanged;

    /// <summary>新邮件到达（供自动化触发器订阅）。</summary>
    public event EventHandler<MailMessage>? NewMailReceived;

    /// <summary>上次同步时间。</summary>
    public DateTimeOffset? LastSyncTime { get; private set; }

    /// <summary>是否正在同步。</summary>
    public bool IsSyncing { get; private set; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 启动后立即同步一次，随后按配置间隔循环
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件同步过程发生未处理异常");
            }

            var intervalMinutes = Math.Max(1, _config.Data.PollIntervalMinutes);
            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    /// <summary>手动触发一次同步（供 UI 刷新调用）。</summary>
    public async Task SyncOnceAsync(CancellationToken ct = default)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, ct))
        {
            RaiseSyncStatus("同步进行中…");
            return;
        }

        try
        {
            IsSyncing = true;
            var accounts = _config.Data.Accounts.Where(a => a.Enabled &&
                !string.IsNullOrWhiteSpace(a.Email) && !string.IsNullOrWhiteSpace(a.ImapServer)).ToList();

            RaiseSyncStatus(accounts.Count == 0 ? "无启用账号" : $"正在同步 {accounts.Count} 个账号…");

            foreach (var account in accounts)
            {
                await SyncAccountAsync(account, ct);
            }

            // 清理已删除账号的水位，避免字典无限增长
            var activeIds = _config.Data.Accounts.Select(a => a.Id).ToHashSet();
            foreach (var staleId in _lastUidByAccount.Keys.Where(id => !activeIds.Contains(id)).ToList())
            {
                _lastUidByAccount.Remove(staleId);
            }

            LastSyncTime = DateTimeOffset.Now;
            RaiseSyncStatus($"同步完成（{DateTime.Now:HH:mm:ss}）");
        }
        finally
        {
            IsSyncing = false;
            _syncLock.Release();
        }
    }

    private async Task SyncAccountAsync(MailAccountSettings account, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(account.EncryptedPassword))
            {
                _logger.LogDebug("账号 {Account} 未设置授权码，跳过", MaskEmail(account.Email));
                return;
            }

            string plainPassword;
            try
            {
                plainPassword = _config.Protector.Decrypt(account.EncryptedPassword);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "账号 {Account} 授权码解密失败，跳过", MaskEmail(account.Email));
                RaiseSyncStatus($"账号 {account.DisplayName} 授权码解密失败，请重新填写");
                return;
            }

            // 轮询只取摘要，再为真正的新邮件补正文，避免每轮下载全部正文
            var mails = await _mail.FetchRecentAsync(
                account, plainPassword, _config.Data.MailCountLimit, includeBodies: false, ct);

            var prefix = account.Id;

            // 首轮同步只建立水位，不推送任何提醒；否则除最旧一封外的全部邮件都会被当作新邮件推送
            var isFirstSync = !_lastUidByAccount.TryGetValue(prefix, out var lastUid);
            var watermark = isFirstSync ? 0u : lastUid;

            var newMails = new List<MailMessage>();
            foreach (var mail in mails.OrderBy(m => m.Uid))
            {
                if (!isFirstSync && mail.Uid > watermark) newMails.Add(mail);
                if (mail.Uid > watermark) watermark = mail.Uid;
            }

            if (newMails.Count > 0)
            {
                // 仅拉取新邮件正文；失败则不推进水位，下一轮重试
                await _mail.FillBodiesAsync(account, plainPassword, newMails, ct);

                // 自动化按每封新邮件触发，不受提醒开关与免打扰影响
                foreach (var mail in newMails)
                {
                    var capturedMail = mail;
                    RunOnUiThread(() =>
                    {
                        try
                        {
                            NewMailReceived?.Invoke(this, capturedMail);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "触发新邮件自动化失败");
                        }
                    });
                }

                var capturedAccount = account;
                var capturedMails = newMails.ToList();
                RunOnUiThread(() => NotifyNewMails(capturedAccount, capturedMails));
            }

            _lastUidByAccount[prefix] = watermark;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "账号 {Account} 同步失败", MaskEmail(account.Email));
            RaiseSyncStatus($"账号 {account.DisplayName} 同步失败");
        }
    }

    /// <summary>按提醒规则推送新邮件；受全局开关、账号开关与免打扰时段约束。</summary>
    private void NotifyNewMails(MailAccountSettings account, IReadOnlyList<MailMessage> mails)
    {
        if (!_config.Data.NotifyEnabled || !account.NotifyEnabled) return;

        if (_config.Data.QuietHoursEnabled &&
            QuietHours.IsQuietTime(DateTime.Now, _config.Data.QuietHoursStart, _config.Data.QuietHoursEnd))
        {
            return;
        }

        var matched = new List<(MailMessage Mail, string? Keyword)>();
        foreach (var mail in mails)
        {
            var (hit, keyword) = MatchKeyword(mail);
            if (hit) matched.Add((mail, keyword));
        }

        if (matched.Count == 0) return;

        // 多封新邮件可合并为一条，避免连续弹出刷屏
        if (_config.Data.NotifySummaryMode && matched.Count > 1)
        {
            _notifier.NotifySummary(account, matched.Select(item => item.Mail).ToList(), _config.Data);
            return;
        }

        foreach (var (mail, keyword) in matched)
        {
            _notifier.Notify(mail, account, _config.Data, !string.IsNullOrEmpty(keyword), keyword);
        }
    }

    private (bool Hit, string? Keyword) MatchKeyword(MailMessage mail)
    {
        var rules = _config.Data.KeywordRules.Where(rule => rule.Enabled).ToList();
        if (rules.Count == 0)
        {
            // 未配置关键词：默认全量提醒
            return (true, null);
        }

        var candidate = new MailCandidate(mail.SenderName, mail.SenderEmail, mail.Subject, mail.TextBody);
        foreach (var rule in rules)
        {
            if (KeywordMatcher.Matches(candidate, new[] { rule }))
            {
                return (true, rule.Keyword);
            }
        }

        return (false, null);
    }

    /// <summary>在 UI 线程上执行操作。已在 UI 线程则直接执行，否则通过 Dispatcher 投递。</summary>
    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            SafeInvoke(action);
            return;
        }

        // 应用退出时 Dispatcher 可能已停止，投递异常忽略即可
        try
        {
            Dispatcher.UIThread.Post(() => SafeInvoke(action));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "向 UI 线程投递操作失败");
        }
    }

    /// <summary>执行回调并兜底异常，避免 UI 线程未处理异常导致宿主崩溃。</summary>
    private void SafeInvoke(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UI 线程提醒回调执行失败");
        }
    }

    /// <summary>日志脱敏：保留首字符与域名。</summary>
    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "(未知)";

        var at = email.IndexOf('@');
        if (at <= 1) return "***";

        return $"{email[0]}***{email[at..]}";
    }

    /// <summary>在 UI 线程上广播同步状态（供 UI 展示）。</summary>
    private void RaiseSyncStatus(string text) =>
        RunOnUiThread(() => SyncStatusChanged?.Invoke(this, text));
}