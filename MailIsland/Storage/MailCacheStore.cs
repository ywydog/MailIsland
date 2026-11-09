using System.Text.Json;
using MailIsland.Models;
using Microsoft.Extensions.Logging;

namespace MailIsland.Storage;

/// <summary>
/// 最近邮件的本地缓存，按账号分别存放在 <c>Cache/MailIsland/Mails</c>。
/// <para>缓存仅用于网络不可用时兜底展示，可随时删除，不属于权威数据。</para>
/// </summary>
public sealed class MailCacheStore
{
    private const string MailsFolderName = "Mails";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private readonly ILogger<MailCacheStore> _logger;

    public MailCacheStore(ILogger<MailCacheStore> logger) => _logger = logger;

    /// <summary>指定账号的缓存文件路径。</summary>
    public string GetCachePath(string accountId)
    {
        var safeId = SanitizeFileName(accountId);
        return Path.Combine(DataPaths.GetDataRoot(), MailsFolderName, safeId + ".json");
    }

    /// <summary>写入账号的最近邮件（先写临时文件再替换，避免中途退出留下损坏文件）。</summary>
    public async Task SaveAsync(string accountId, IReadOnlyList<MailMessage> mails, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return;

        try
        {
            var path = GetCachePath(accountId);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            var tempPath = path + ".tmp";
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, mails, JsonOptions, ct);
            }

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

    /// <summary>读取账号缓存；不存在或损坏时返回 null。</summary>
    public async Task<List<MailMessage>?> TryLoadAsync(string accountId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(accountId)) return null;

        var path = GetCachePath(accountId);
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<MailMessage>>(stream, JsonOptions, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读取邮件缓存失败：{AccountId}", accountId);
            return null;
        }
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var name = new string(chars);
        return string.IsNullOrWhiteSpace(name) ? "unknown" : name;
    }
}
