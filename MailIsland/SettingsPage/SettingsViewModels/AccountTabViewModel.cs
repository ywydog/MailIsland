using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MailIsland.ConfigHandlers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Presets;
using MailIsland.Logic.Shared;
using MailIsland.Services;

namespace MailIsland.SettingsPage.SettingsViewModels;

/// <summary>账号页 ViewModel。</summary>
public partial class AccountTabViewModel : ObservableObject
{
    private readonly MailIslandConfigHandler _config;
    private readonly IMailClientService _mail;

    [ObservableProperty] private MailAccountSettings? _current;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private string _passwordPlain = "";

    public AccountTabViewModel(MailIslandConfigHandler config, IMailClientService mail)
    {
        _config = config;
        _mail = mail;
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
            Status = "连接成功 ✓";
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
        if (!string.IsNullOrWhiteSpace(PasswordPlain))
        {
            acc.EncryptedPassword = _config.Protector.Encrypt(PasswordPlain);
            _config.Save();
            Status = "授权码已保存。";
        }
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
        Accounts.Remove(acc);
        Current = Accounts.FirstOrDefault();
        SaveAccounts();
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
        acc.DisplayName = preset.Kind == PresetKind.Custom ? acc.DisplayName : preset.DisplayName;
        Status = $"已应用预设：{preset.DisplayName}";
    }

    /// <summary>把 Accounts 变更回写到配置并保存。</summary>
    public void SaveAll() => SaveAccounts();

    /// <summary>把当前列表回写配置并持久化（增删的单一保存入口，避免遗漏）。</summary>
    private void SaveAccounts()
    {
        _config.Data.Accounts = Accounts.ToList();
        _config.Save();
    }
}