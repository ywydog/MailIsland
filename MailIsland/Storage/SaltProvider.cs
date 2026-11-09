using System.Security.Cryptography;

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
    public static byte[]? GetOrCreate(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return null;

        lock (SyncRoot)
        {
            if (_cached is { Length: > 0 }) return _cached;

            try
            {
                Directory.CreateDirectory(folder);
                var path = Path.Combine(folder, SaltFileName);

                if (File.Exists(path))
                {
                    var saved = Convert.FromBase64String(File.ReadAllText(path).Trim());
                    if (saved.Length >= SaltSize)
                    {
                        _cached = saved;
                        return _cached;
                    }
                }

                var salt = RandomNumberGenerator.GetBytes(SaltSize);
                File.WriteAllText(path, Convert.ToBase64String(salt));
                _cached = salt;
                return _cached;
            }
            catch
            {
                // 盐不可用时不阻断插件运行，退化为无盐加密
                return null;
            }
        }
    }
}
