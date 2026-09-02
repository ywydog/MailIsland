using ClassIsland.Shared.Helpers;
using MailIsland.Logic.Config;
using MailIsland.Logic.Security;
using MailIsland.Shared;

namespace MailIsland.ConfigHandlers;

/// <summary>邮箱插件配置处理器：JSON 持久化，授权码经凭据保护器加密。</summary>
public sealed class MailIslandConfigHandler
{
    private readonly string _configPath;
    private readonly ICredentialProtector _protector;

    public MailIslandConfigHandler()
    {
        _configPath = Path.Combine(GlobalConstants.PluginConfigFolder, "MailIslandConfig.json");
        _protector = OperatingSystem.IsWindows()
            ? new WindowsCredentialProtector()
            : new NullCredentialProtector();
        Data = Load();
    }

    public MailIslandConfigData Data { get; private set; }

    /// <summary>获取当前凭据保护器（供 UI 查看/设置授权码明文）。</summary>
    public ICredentialProtector Protector => _protector;

    public void Save() => ConfigureFileHelper.SaveConfig(_configPath, Data);

    private MailIslandConfigData Load()
    {
        var data = ConfigureFileHelper.LoadConfig<MailIslandConfigData>(_configPath);

        // 校验加密字段可解，否则置空待重填
        foreach (var acc in data.Accounts)
        {
            if (string.IsNullOrEmpty(acc.EncryptedPassword)) continue;
            try
            {
                _protector.Decrypt(acc.EncryptedPassword);
            }
            catch
            {
                acc.EncryptedPassword = "";
            }
        }

        return data;
    }
}