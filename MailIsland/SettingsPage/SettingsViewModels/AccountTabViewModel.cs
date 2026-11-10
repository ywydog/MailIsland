using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Presets;
using MailIsland.Logic.Shared;
using MailIsland.Services;
using MailIsland.Storage;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>账号页 ViewModel。</summary>
public partial class AccountTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;
    private readonly ISmtpClientService _smtp;
    private readonly MailCacheStore _cache;

    [ObservableProperty] private MailAccountSettings? _current;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private string _passwordPlain = "";

    public AccountTabViewModel(
        MailIslandConfigHandler config,
        IMailClientService mail,
        MailCacheStore cache,
        ISmtpClientService smtp)
    {
        _config = config;
        _mail = mail;
        _cache = cache;
        _smtp = smtp;
        Accounts = new ObservableCollection<MailAccountSettings>(config.Data.Accounts);
    }

    public ObservableCollection<MailAccountSettings> Accounts { get; }

    /// <summary>国内邮箱预设列表（含「自定义」）。</summary>
    public IReadOnlyList<MailPreset> Presets => MailPresetCatalog.Presets;

    public async Task TestConnectionAsync()
    {
        if (Current is not { } acc)
        {
            Status = "请先选择或新建一个账号。";
            return;
        }
        if (string.IsNullOrWhiteSpace(acc.Email) || string.IsNullOrWhiteSpace(acc.ImapServer) ||
            string.IsNullOrWhiteSpace(PasswordPlain))
        {
            Status = "请填写邮箱、IMAP 服务器与授权码后再验证。";
            return;
        }

        IsTesting = true;
        Status = "正在验证连接…";
        try
        {
            await _mail.VerifyConnectionAsync(acc, PasswordPlain, CancellationToken.None);

            if (!string.IsNullOrWhiteSpace(acc.SmtpServer))
            {
                await _smtp.VerifyConnectionAsync(acc, PasswordPlain, CancellationToken.None);
                Status = "收件与发件连接均成功 ✓";
            }
            else
            {
                Status = "收件连接成功 ✓（未配置 SMTP）";
            }
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsTesting = false;
        }
    }

    /// <summary>将当前授权码写入加密字段并保存。</summary>
    public void ApplyPasswordAndSave()
    {
        if (Current is not { } acc) return;
        if (string.IsNullOrWhiteSpace(PasswordPlain)) return;

        acc.EncryptedPassword = _config.Protector.Encrypt(PasswordPlain);
        if (!_config.Save())
        {
            Status = "保存失败，请检查配置文件权限或磁盘空间。";
            return;
        }

        PasswordPlain = "";
        Status = "授权码已保存。";
    }

    /// <summary>新增账号并选中。</summary>
    public void AddAccount()
    {
        var acc = new MailAccountSettings { DisplayName = "新账号", IsSelected = true };
        // 取消其他选中
        foreach (var a in Accounts) a.IsSelected = false;
        Accounts.Add(acc);
        Current = acc;
        SaveAccounts();
    }

    /// <summary>删除当前账号。</summary>
    public void DeleteAccount()
    {
        if (Current is not { } acc) return;

        var deletedId = acc.Id;
        Accounts.Remove(acc);
        Current = Accounts.FirstOrDefault();
        SaveAccounts();

        // 同步清除该账号的本地邮件缓存，避免隐私数据残留
        _cache.Delete(deletedId);
    }

    /// <summary>根据预设填充服务器信息。</summary>
    public void ApplyPreset(MailPreset preset)
    {
        if (Current is not { } acc) return;

        acc.ImapServer = preset.ImapServer;
        acc.ImapPort = preset.ImapPort;
        acc.ImapUseSsl = preset.ImapSsl;
        acc.SmtpServer = preset.SmtpServer;
        acc.SmtpPort = preset.SmtpPort;
        acc.SmtpUseSsl = preset.SmtpSsl;

        // 仅在用户尚未命名时填充显示名，避免覆盖自定义名称
        if (preset.Kind != PresetKind.Custom &&
            (string.IsNullOrWhiteSpace(acc.DisplayName) || acc.DisplayName == "新账号"))
        {
            acc.DisplayName = preset.DisplayName;
        }

        AutoSave();
        Status = $"已应用预设：{preset.DisplayName}";
    }

    /// <summary>把 Accounts 变更回写到配置并保存（显示成功提示）。</summary>
    public void SaveAll() => Persist(showSuccess: true);

    /// <summary>自动保存字段与开关变化；仅在失败时提示。</summary>
    public void AutoSave() => Persist(showSuccess: false);

    private void Persist(bool showSuccess)
    {
        _config.Data.Accounts = Accounts.ToList();

        if (_config.Save())
        {
            if (showSuccess) Status = "已保存。";
            return;
        }

        Status = "保存失败，请检查配置文件权限或磁盘空间。";
    }

    /// <summary>把当前列表回写配置并持久化（增删的单一保存入口，避免遗漏）。</summary>
    private void SaveAccounts() => Persist(showSuccess: false);
}