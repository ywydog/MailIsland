using HtmlAgilityPack;

namespace MailIsland.Logic.Html;

/// <summary>HTML 净化器：移除脚本/事件属性/危险 URI，返回净化后 HTML。</summary>
public static class HtmlSanitizer
{
    private static readonly HashSet<string> AllowedHrefSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto",
    };

    /// <summary>src 白名单：仅允许 http/https。不放行 data:，避免任意内联图片（追踪像素/隐私）随正文加载。</summary>
    private static readonly HashSet<string> AllowedSrcSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https",
    };

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var doc = new HtmlDocument();
        doc.OptionOutputOriginalCase = false;
        doc.LoadHtml(html);

        foreach (var node in doc.DocumentNode.Descendants().ToList())
        {
            if (node.NodeType == HtmlNodeType.Comment)
            {
                node.Remove();
                continue;
            }

            // script/style 整体删除（含内容），iframe/object/embed 删除节点但保留子内容文本
            var name = node.Name.ToLowerInvariant();
            if (name is "script" or "style")
            {
                node.Remove();
                continue;
            }
            if (name is "iframe" or "object" or "embed" or "form" or "input" or "button")
            {
                node.ParentNode?.RemoveChild(node, false);
                continue;
            }

            // 移除事件属性（on*）
            var attrsToRemove = node.Attributes
                .Where(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var a in attrsToRemove) node.Attributes.Remove(a);

            // href 只允许 http/https/mailto
            var href = node.Attributes["href"];
            if (href != null)
            {
                var val = href.Value.Trim();
                if (!IsAllowedUri(val, AllowedHrefSchemes))
                    node.Attributes.Remove("href");
            }

            // src 只允许 http/https
            var src = node.Attributes["src"];
            if (src != null)
            {
                var val = src.Value.Trim();
                if (!IsAllowedUri(val, AllowedSrcSchemes))
                    node.Attributes.Remove("src");
            }
        }

        return doc.DocumentNode.WriteTo();
    }

    private static bool IsAllowedUri(string value, HashSet<string> allowedSchemes)
    {
        // 相对路径（同站链接）放行；绝对路径需 scheme 白名单
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return allowedSchemes.Contains(uri.Scheme);
        // 相对链接（如 /path、./x、#anchor）安全放行
        return !value.StartsWith("//", StringComparison.Ordinal) &&
               !value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase);
    }
}