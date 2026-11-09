using MailIsland.Shared;

namespace MailIsland.Storage;

/// <summary>
/// 插件数据目录。
/// <para>沿用 SystemTools 的目录约定：位于 ClassIsland 数据根的
/// <c>Cache/MailIsland</c>，由插件目录上溯两级得到，兼容便携版与安装版。</para>
/// <para>注意：ClassIsland 的恢复功能会清空整个 <c>Cache</c> 目录，
/// 因此这里只存放可重新生成的缓存数据，配置仍保存在 PluginConfigFolder。</para>
/// </summary>
public static class DataPaths
{
    private const string CacheFolderName = "Cache";
    private const string PluginFolderName = "MailIsland";

    /// <summary>计算插件数据根目录。插件目录为空时退回到配置目录，避免路径落错位置。</summary>
    public static string GetDataRoot(string pluginFolder)
    {
        if (string.IsNullOrWhiteSpace(pluginFolder))
        {
            return GlobalConstants.PluginConfigFolder;
        }

        return Path.GetFullPath(Path.Combine(pluginFolder, "..", "..", CacheFolderName, PluginFolderName));
    }

    /// <summary>当前运行时数据根目录。</summary>
    public static string GetDataRoot() => GetDataRoot(GlobalConstants.PluginFolder);
}
