using System.Text;
using System.Text.RegularExpressions;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using HtmlAgilityPack;
using MailIsland.Logic.Html;

namespace MailIsland.Services;

/// <summary>
/// 把净化后的 HTML 转换为一组可显示的 Avalonia 元素，适配深浅色。
/// <para>规则：段落→TextBlock 换行；开启图片加载时，正文中的 http/https 图片会异步下载并显示（限制数量与大小）。</para>
/// </summary>
public static class HtmlToAvaloniaRenderer
{
    private const int MaxImagesPerMail = 20;

    /// <summary>构建邮件正文展示面板（无正文时返回提示文本）。</summary>
    public static Control Build(
        string? html,
        string? fallbackPlainText = null,
        bool loadRemoteImages = false,
        InlineImageResolver? inlineResolver = null)
    {
        var clean = HtmlSanitizer.Sanitize(html);

        var panel = new StackPanel { Spacing = 8 };

        if (string.IsNullOrWhiteSpace(clean))
        {
            var emptyText = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(fallbackPlainText) ? "(无正文)" : fallbackPlainText,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 14,
            };
            emptyText[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextFillColorPrimaryBrush");
            panel.Children.Add(emptyText);
            return panel;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(clean);

        var nodes = doc.DocumentNode.SelectNodes("//body/*") ?? doc.DocumentNode.ChildNodes;
        var imageBudget = MaxImagesPerMail;
        foreach (var node in nodes.Where(n => n.NodeType == HtmlNodeType.Element))
        {
            panel.Children.Add(BuildBlock(node, loadRemoteImages, ref imageBudget, inlineResolver));
        }
        return panel;
    }

    private static Control BuildBlock(
        HtmlNode node, bool loadImages, ref int imageBudget, InlineImageResolver? inlineResolver)
    {
        if (loadImages && node.Name.Equals("img", StringComparison.OrdinalIgnoreCase))
        {
            return TryCreateImage(node, ref imageBudget, inlineResolver) ?? MakeText("", 14);
        }

        if (loadImages && HasImage(node))
        {
            var stack = new StackPanel { Spacing = 6 };
            RenderInline(node, stack, fontSize: 14, ref imageBudget, inlineResolver);
            if (stack.Children.Count == 0)
            {
                stack.Children.Add(MakeText(GetNodeText(node), 14));
            }
            return stack;
        }

        var text = GetNodeText(node);
        return node.Name.ToLowerInvariant() switch
        {
            "h1" or "h2" or "h3" or "h4" => MakeText(text, 18, bold: true),
            "li" => MakeText("• " + text, 14),
            "blockquote" => MakeText(text, 13, italic: true),
            _ => MakeText(text, 14),
        };
    }

    /// <summary>按文档顺序输出文本与图片，块级元素之间换行。</summary>
    private static void RenderInline(
        HtmlNode node, StackPanel panel, double fontSize, ref int imageBudget, InlineImageResolver? inlineResolver)
    {
        var buffer = new StringBuilder();

        void Flush()
        {
            var text = NormalizeInline(buffer.ToString());
            buffer.Clear();
            if (text.Length > 0) panel.Children.Add(MakeText(text, fontSize));
        }

        foreach (var child in node.ChildNodes)
        {
            if (child.NodeType == HtmlNodeType.Text)
            {
                buffer.Append(child.InnerText);
                continue;
            }
            if (child.NodeType != HtmlNodeType.Element) continue;

            var name = child.Name.ToLowerInvariant();
            if (name is "script" or "style") continue;

            if (name == "br")
            {
                buffer.Append('\n');
                continue;
            }

            if (name == "img")
            {
                Flush();
                var image = TryCreateImage(child, ref imageBudget, inlineResolver);
                if (image != null) panel.Children.Add(image);
                continue;
            }

            if (name is "p" or "div" or "li" or "tr" or "table" or "blockquote" or "h1" or "h2" or "h3" or "h4")
            {
                Flush();
                RenderInline(child, panel, fontSize, ref imageBudget, inlineResolver);
                Flush();
                continue;
            }

            RenderInline(child, panel, fontSize, ref imageBudget, inlineResolver);
        }

        Flush();
    }

    private static Control? TryCreateImage(HtmlNode node, ref int imageBudget, InlineImageResolver? inlineResolver)
    {
        if (imageBudget <= 0) return null;

        var src = node.GetAttributeValue("src", "").Trim();
        var alt = node.GetAttributeValue("alt", "");

        // 正文内嵌图片：cid: 引用通过 MIME 部件按需下载
        if (src.StartsWith("cid:", StringComparison.OrdinalIgnoreCase))
        {
            if (inlineResolver == null) return null;

            var contentId = src[4..];
            if (!inlineResolver.Contains(contentId)) return null;

            imageBudget--;
            var inlineImage = CreateImage(alt);
            _ = LoadInlineImageAsync(inlineImage, contentId, inlineResolver);
            return inlineImage;
        }

        if (!Uri.TryCreate(src, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

        imageBudget--;

        var image = CreateImage(alt);
        _ = LoadImageAsync(image, uri);
        return image;
    }

    private static Image CreateImage(string alt)
    {
        var image = new Image
        {
            MaxWidth = 520,
            Stretch = Stretch.Uniform,
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        if (!string.IsNullOrWhiteSpace(alt)) ToolTip.SetTip(image, alt);
        return image;
    }

    private static async Task LoadImageAsync(Image image, Uri uri)
    {
        var bitmap = await RemoteImageLoader.LoadAsync(uri).ConfigureAwait(false);
        if (bitmap == null) return;

        await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
    }

    private static async Task LoadInlineImageAsync(Image image, string contentId, InlineImageResolver resolver)
    {
        try
        {
            var bytes = await resolver.LoadAsync(contentId).ConfigureAwait(false);
            if (bytes is not { Length: > 0 }) return;

            using var stream = new MemoryStream(bytes);
            var bitmap = new Bitmap(stream);
            await Dispatcher.UIThread.InvokeAsync(() => image.Source = bitmap);
        }
        catch
        {
            // 单个内嵌图片失败不影响正文
        }
    }

    private static bool HasImage(HtmlNode node) =>
        node.Name.Equals("img", StringComparison.OrdinalIgnoreCase) || node.Descendants("img").Any();

    private static string GetNodeText(HtmlNode node) =>
        HtmlEntity.DeEntitize(node.InnerText.Trim());

    private static string NormalizeInline(string text)
    {
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = text.Split('\n')
            .Select(line => Regex.Replace(line, "[ \\t\\f\\v]+", " ").Trim());

        return string.Join('\n', lines).Trim();
    }

    private static TextBlock MakeText(string text, double fontSize, bool bold = false, bool italic = false)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };
        tb[!TextBlock.ForegroundProperty] = new DynamicResourceExtension("TextFillColorPrimaryBrush");
        tb.Text = text;
        return tb;
    }
}
