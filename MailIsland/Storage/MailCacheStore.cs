using System.Text.Json;
using MailIsland.ConfigHandlers;
using MailIsland.Models;
using Microsoft.Extensions.Logging;

namespace MailIsland.Storage;

/// <summary>
/// 最近邮件的本地缓存，按账号分别存放在 <c>Cache/MailIsland/Mails</c>。
/// <para>缓存含发件人、主题与正文等隐私数据，落盘前经带盐的凭据保护器加密（Windows 为 DPAPI + 随机盐）。</para>
/// <para>缓存仅用于网络不可用时兜底展示，可随时删除，不属于权威数据。</para>
/// </summary>
public sealed class MailCacheStore
{
    private const string MailsFolderName = "Mails";
    private const string CacheExtension = ".dat";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ILogger<MailCacheStore> _logger;
    private readonly MailIslandConfigHandler _config;

    public MailCacheStore(ILogger<MailCacheStore> logger, MailIslandConfigHandler config)
    {
        _logger = logger;
        _config = config;
        PurgeLegacyPlaintextCache();
    }

    /// <summary>指定账号的缓存文件路径。</summary>
    public string GetCachePath(string accountId)
    {
        var safeId = SanitizeFileName(accountId);
        return Path.Combine(DataPaths.GetDataRoot(), MailsFolderName, safeId + CacheExtension);
    }

    /// <summary>写入账号的最近邮件（加密后先写临时文件再替换，避免中途退出留下损坏文件）。</summary>
    public async Task SaveAsync(string accountId, IReadOnlyList<MailMessage> mails, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;

        try
        {
            var path = GetCachePath(accountId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var json = JsonSerializer.Serialize(mails, JsonOptions);
            var payload = _config.Protector.Encrypt(json);

            var tempPath = path + ".tmp";
            await File.WriteAllTextAsync(tempPath, payload, ct).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入邮件缓存失败：{AccountId}", accountId);
        }
    }

    /// <summary>删除指定账号的缓存（删除账号时调用，避免隐私数据残留）。</summary>
    public void Delete(string accountId)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;

        TryDelete(GetCachePath(accountId));
    }

    /// <summary>读取并解密账号缓存；不存在或无法解密时返回 null。</summary>
    public async Task<List<MailMessage>?> TryLoadAsync(string accountId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return null;

        var path = GetCachePath(accountId);
        if (!File.Exists(path)) return null;

        try
        {
            var payload = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var json = _config.Protector.Decrypt(payload);
            return JsonSerializer.Deserialize<List<MailMessage>>(json, JsonOptions);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 密文损坏或盐变化导致无法解开时直接丢弃，避免反复失败
            _logger.LogWarning(ex, "读取邮件缓存失败，已丢弃：{AccountId}", accountId);
            TryDelete(path);
            return null;
        }
    }

    /// <summary>清理旧版本写入的明文 JSON 缓存，避免隐私数据继续以明文留在磁盘上。</summary>
    private void PurgeLegacyPlaintextCache()
    {
        try
        {
            var root = DataPaths.GetDataRoot();
            if (string.IsNullOrWhiteSpace(root)) return;

            var folder = Path.Combine(root, MailsFolderName);
            if (!Directory.Exists(folder)) return;

            foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
            {
                File.Delete(file);
            }

            // 清理异常退出遗留的临时文件
            foreach (var file in Directory.EnumerateFiles(folder, "*.tmp"))
            {
                File.Delete(file);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理旧版明文邮件缓存失败");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // 删除失败不影响主流程，下次读取仍会走异常分支
        }
    }

    /// <summary>账号 Id 只保留字母、数字、短横线、下划线，避免路径穿越与非法文件名。</summary>
    private static string SanitizeFileName(string value)
    {
        var chars = value.Trim()
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')
            .ToArray();

        var name = new string(chars);
        if (name.Length == 0) return "unknown";
        return name.Length <= 64 ? name : name[..64];
    }
}
