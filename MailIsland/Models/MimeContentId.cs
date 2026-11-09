namespace MailIsland.Models;

/// <summary>Content-ID 规范化工具。</summary>
public static class MimeContentId
{
    /// <summary>去掉尖括号与空白，便于 cid: 引用与 BODYSTRUCTURE 中的 Content-ID 对齐。</summary>
    public static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().Trim('<', '>').Trim();
}
