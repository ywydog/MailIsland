namespace MailIsland.Storage;

/// <summary>附件保存位置与文件名处理。默认保存到桌面。</summary>
public static class AttachmentStorage
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>默认下载目录：桌面；不可用时退回用户目录，最后退回插件数据目录。</summary>
    public static string GetDownloadDirectory()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrWhiteSpace(desktop)) return desktop;

        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(profile)) return profile;

        return DataPaths.GetDataRoot();
    }

    /// <summary>在目标目录中生成不冲突的保存路径。</summary>
    public static string BuildTargetPath(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);

        var safeName = SanitizeFileName(fileName);
        var path = Path.Combine(directory, safeName);
        if (!File.Exists(path)) return path;

        var stem = Path.GetFileNameWithoutExtension(safeName);
        var extension = Path.GetExtension(safeName);

        for (var i = 1; i < 1000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }

        return Path.Combine(directory, $"{stem}-{Guid.NewGuid():N}{extension}");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = fileName.Trim()
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray();

        var name = new string(chars).Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(name)) name = "attachment";

        var stem = Path.GetFileNameWithoutExtension(name);
        if (ReservedNames.Contains(stem)) name = "_" + name;

        return name.Length <= 120 ? name : name[..120];
    }
}
