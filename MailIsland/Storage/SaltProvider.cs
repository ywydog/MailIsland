using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace MailIsland.Storage;

/// <summary>
/// 插件实例盐值：首次使用时随机生成并持久化，之后作为 DPAPI 的 entropy 参与加解密。
/// <para>盐保存在配置目录（不随 Cache 清理），保证缓存与授权码在插件升级后仍可解开。</para>
/// </summary>
public static class SaltProvider
{
    private const string SaltFileName = ".mailisland-salt";
    private const int SaltSize = 32;

    private static readonly object SyncRoot = new();
    private static byte[]? _cached;

    /// <summary>获取或创建盐值；目录不可用时返回 null（此时不启用盐）。</summary>
    public static byte[]? GetOrCreate(string folder, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;

        lock (SyncRoot)
        {
            if (_cached is { Length: > 0 }) return _cached;

            var path = Path.Combine(folder, SaltFileName);

            try
            {
                Directory.CreateDirectory(folder);

                if (File.Exists(path))
                {
                    try
                    {
                        var saved = Convert.FromBase64String(File.ReadAllText(path).Trim());
                        if (saved.Length >= SaltSize)
                        {
                            _cached = saved;
                            return _cached;
                        }

                        logger?.LogWarning("盐文件长度异常，将重新生成：{Path}", path);
                    }
                    catch (Exception ex)
                    {
                        logger?.LogWarning(ex, "盐文件损坏，将重新生成：{Path}", path);
                    }

                    TryDelete(path);
                }

                var salt = RandomNumberGenerator.GetBytes(SaltSize);

                // 先写临时文件再替换，避免中途退出留下半个文件
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, Convert.ToBase64String(salt));
                File.Move(tempPath, path, overwrite: true);

                logger?.LogInformation("已生成插件盐值：{Path}", path);
                _cached = salt;
                return _cached;
            }
            catch (Exception ex)
            {
                // 盐不可用时不阻断插件运行，退化为无盐加密
                logger?.LogWarning(ex, "无法读取或写入盐文件，加密将退化为无盐模式");
                return null;
            }
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
            // 删除失败时后续写入会覆盖
        }
    }
}
