namespace MailIsland.Models;

/// <summary>字节大小格式化。</summary>
public static class ByteSize
{
    public static string Format(long size)
    {
        if (size <= 0) return "0 B";
        if (size < 1024) return $"{size} B";
        if (size < 1024 * 1024) return $"{size / 1024.0:0.#} KB";
        return $"{size / (1024.0 * 1024):0.#} MB";
    }
}
