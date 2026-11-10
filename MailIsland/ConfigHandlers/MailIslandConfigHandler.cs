using ClassIsland.Shared.Helpers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Security;
using MailIsland.Shared;
using MailIsland.Storage;
using Microsoft.Extensions.Logging;

namespace MailIsland.ConfigHandlers;

/// <summary>
/// 邮箱插件配置处理器。
/// <para>持久化时对隐私字段（邮箱地址、显示名、关键词）及授权码做带盐加密，
/// 内存中的 <see cref="Data"/> 保持明文供界面绑定与服务使用。</para>
/// </summary>
public sealed class MailIslandConfigHandler
{
    private readonly string _configPath;
    private readonly ICredentialProtector _protector;
    private readonly ILogger<MailIslandConfigHandler> _logger;
    private bool _needsSave;

    public MailIslandConfigHandler(ILogger<MailIslandConfigHandler> logger)
    {
        _logger = logger;
        _configPath = Path.Combine(GlobalConstants.PluginConfigFolder, "MailIslandConfig.json");
        var salt = SaltProvider.GetOrCreate(GlobalConstants.PluginConfigFolder);
        _protector = CreateProtector(salt);
        Data = Load();

        // 旧版本明文/无盐密文在加载时已转换，落盘一次完成迁移
        if (_needsSave) Save();
    }

    public MailIslandConfigData Data { get; private set; }

    /// <summary>获取当前凭据保护器（供 UI 设置授权码，以及加密本地邮件缓存）。</summary>
    public ICredentialProtector Protector => _protector;

    /// <summary>保存配置：隐私字段加密后写入磁盘；写入失败不抛出，避免中断 UI 操作。</summary>
    public void Save()
    {
        try
        {
            ConfigureFileHelper.SaveConfig(_configPath, BuildEncryptedSnapshot());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存邮箱插件配置失败");
        }
    }

    private static ICredentialProtector CreateProtector(byte[]? salt) =>
        OperatingSystem.IsWindows()
            ? new WindowsCredentialProtector(salt)
            : new NullCredentialProtector();

    private MailIslandConfigData Load()
    {
        var data = ConfigureFileHelper.LoadConfig<MailIslandConfigData>(_configPath);

        foreach (var acc in data.Accounts)
        {
            acc.Email = Unprotect(acc.Email, ref _needsSave);
            acc.DisplayName = Unprotect(acc.DisplayName, ref _needsSave);
            acc.Signature = Unprotect(acc.Signature, ref _needsSave);
            acc.EncryptedPassword = MigratePassword(acc.EncryptedPassword);
        }

        foreach (var rule in data.KeywordRules)
        {
            rule.Keyword = Unprotect(rule.Keyword, ref _needsSave);
        }

        return data;
    }

    /// <summary>构造用于落盘的快照，隐私字段全部替换为密文。</summary>
    private MailIslandConfigData BuildEncryptedSnapshot() => new()
    {
        PollIntervalMinutes = Data.PollIntervalMinutes,
        MailCountLimit = Data.MailCountLimit,
        NotifyEnabled = Data.NotifyEnabled,
        OverlayContentKind = Data.OverlayContentKind,
        MaskContentKind = Data.MaskContentKind,
        DisplayMode = Data.DisplayMode,
        StaticDisplaySeconds = Data.StaticDisplaySeconds,
        ScrollCharsPerSecond = Data.ScrollCharsPerSecond,
        NotificationSpeechEnabled = Data.NotificationSpeechEnabled,
        LoadBodyImages = Data.LoadBodyImages,
        SaveSentCopy = Data.SaveSentCopy,
        NotifySummaryMode = Data.NotifySummaryMode,
        QuietHoursEnabled = Data.QuietHoursEnabled,
        QuietHoursStart = Data.QuietHoursStart,
        QuietHoursEnd = Data.QuietHoursEnd,
        Accounts = Data.Accounts.Select(a => new MailAccountSettings
        {
            Id = a.Id,
            DisplayName = Protect(a.DisplayName),
            Email = Protect(a.Email),
            ImapServer = a.ImapServer,
            ImapPort = a.ImapPort,
            ImapUseSsl = a.ImapUseSsl,
            SmtpServer = a.SmtpServer,
            SmtpPort = a.SmtpPort,
            SmtpUseSsl = a.SmtpUseSsl,
            EncryptedPassword = a.EncryptedPassword,
            Enabled = a.Enabled,
            IsSelected = a.IsSelected,
            NotifyEnabled = a.NotifyEnabled,
            Signature = Protect(a.Signature),
            QuoteOriginal = a.QuoteOriginal,
        }).ToList(),
        KeywordRules = Data.KeywordRules.Select(r => new KeywordRule
        {
            Keyword = Protect(r.Keyword),
            Scope = r.Scope,
            Enabled = r.Enabled,
        }).ToList(),
    };

    private string Protect(string value) =>
        string.IsNullOrEmpty(value) ? "" : _protector.Encrypt(value);

    /// <summary>
    /// 解密字段。旧版本明文（非 Base64 形态）原样返回并标记需要重新加密；
    /// 看起来是密文却解不开（盐丢失/文件损坏）时返回空，避免把乱码当成内容回显。
    /// </summary>
    private string Unprotect(string value, ref bool needsSave)
    {
        if (string.IsNullOrEmpty(value)) return "";

        if (!LooksLikeCipher(value))
        {
            needsSave = true;
            return value;
        }

        try
        {
            return _protector.Decrypt(value);
        }
        catch
        {
            return "";
        }
    }

    private static bool LooksLikeCipher(string value) =>
        value.Length >= 64 && Convert.TryFromBase64String(value, new byte[value.Length], out _);

    /// <summary>授权码：新盐可解则保留；否则尝试旧版无盐密文并按新盐重新加密；都失败则清空待重填。</summary>
    private string MigratePassword(string cipherText)
    {
        if (string.IsNullOrEmpty(cipherText)) return "";

        try
        {
            _protector.Decrypt(cipherText);
            return cipherText;
        }
        catch
        {
            // 继续尝试迁移
        }

        if (!OperatingSystem.IsWindows()) return "";

        try
        {
            var plain = new WindowsCredentialProtector().Decrypt(cipherText);
            _needsSave = true;
            return _protector.Encrypt(plain);
        }
        catch
        {
            return "";
        }
    }
}
