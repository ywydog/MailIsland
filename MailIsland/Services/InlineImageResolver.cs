using MailIsland.Models;

namespace MailIsland.Services;

/// <summary>正文内嵌图片解析器：把 cid: 引用映射到 MIME 部件并按需下载。</summary>
public sealed class InlineImageResolver
{
    private readonly IReadOnlyDictionary<string, string> _partSpecifiers;
    private readonly Func<string, Task<byte[]?>> _loadAsync;

    public InlineImageResolver(
        IReadOnlyDictionary<string, string> partSpecifiers,
        Func<string, Task<byte[]?>> loadAsync)
    {
        _partSpecifiers = partSpecifiers;
        _loadAsync = loadAsync;
    }

    /// <summary>判断是否存在对应的内嵌图片部件。</summary>
    public bool Contains(string contentId) =>
        _partSpecifiers.ContainsKey(MimeContentId.Normalize(contentId));

    /// <summary>下载内嵌图片内容；失败时返回 null。</summary>
    public Task<byte[]?> LoadAsync(string contentId) => _loadAsync(contentId);
}
