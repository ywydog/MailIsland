using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Keyword;
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

    /// <summary>同步状态变化：arg = 描述文本。</summary>
    public event EventHandler<string>? SyncStatusChanged;

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
            SyncStatusChanged?.Invoke(this, "同步进行中…");
            return;
        }

        try
        {
            IsSyncing = true;
            var accounts = _config.Data.Accounts.Where(a => a.Enabled &&
                !string.IsNullOrWhiteSpace(a.Email) && !string.IsNullOrWhiteSpace(a.ImapServer)).ToList();

            SyncStatusChanged?.Invoke(this, accounts.Count == 0 ? "无启用账号" : $"正在同步 {accounts.Count} 个账号…");

            foreach (var account in accounts)
            {
                await SyncAccountAsync(account, ct);
            }

            LastSyncTime = DateTimeOffset.Now;
            SyncStatusChanged?.Invoke(this, $"同步完成（{DateTime.Now:HH:mm:ss}）");
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
            var plainPassword = _config.Protector.Decrypt(account.EncryptedPassword);
            if (string.IsNullOrWhiteSpace(plainPassword))
            {
                _logger.LogWarning("账号 {Email} 未设置授权码，跳过", account.Email);
                return;
            }

            var mails = await _mail.FetchRecentAsync(account, plainPassword, _config.Data.MailCountLimit, ct);

            var prefix = account.Id;
            foreach (var mail in mails.OrderBy(m => m.Uid))
            {
                var isFirstSeen = !_lastUidByAccount.TryGetValue(prefix, out var lastUid);

                // 首轮仅建立水位，不提醒；后续仅处理更新的邮件
                if (!isFirstSeen && mail.Uid > lastUid)
                {
                    TryNotify(account, mail);
                }

                _lastUidByAccount[prefix] = isFirstSeen ? mail.Uid : Math.Max(lastUid, mail.Uid);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "账号 {Email} 同步失败", account.Email);
            SyncStatusChanged?.Invoke(this, $"账号 {account.DisplayName} 同步失败");
        }
    }

    private void TryNotify(MailAccountSettings account, MailMessage mail)
    {
        if (!_config.Data.NotifyEnabled) return;

        var rules = _config.Data.KeywordRules.Where(r => r.Enabled).ToList();
        var hitKeyword = (string?)null;
        var hit = false;

        if (rules.Count == 0)
        {
            // 未配置关键词：默认全量提醒
            hit = true;
        }
        else
        {
            var candidate = new MailCandidate(mail.SenderName, mail.SenderEmail, mail.Subject, mail.TextBody);
            foreach (var rule in rules)
            {
                if (RuleMatches(candidate, rule))
                {
                    hit = true;
                    hitKeyword = rule.Keyword;
                    break;
                }
            }
        }

        if (hit)
        {
            _notifier.Notify(mail, account, !string.IsNullOrEmpty(hitKeyword), hitKeyword);
        }
    }

    private static bool RuleMatches(MailCandidate candidate, KeywordRule rule) =>
        KeywordMatcher.Matches(candidate, new[] { rule });
}