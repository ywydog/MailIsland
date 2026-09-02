using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using HtmlAgilityPack;
using MailIsland.Logic.Html;

namespace MailIsland.Services;

/// <summary>
/// 把净化后的 HTML 转换为一组可显示的 Avalonia 元素，适配深浅色。
/// <para>规则：段落→TextBlock 换行，链接→可点击 TextBlock（前景用强调色），文本用动态主题资源自适应深浅色。</para>
/// </summary>
public static class HtmlToAvaloniaRenderer
{
    /// <summary>构建邮件正文展示面板（无正文时返回提示文本）。</summary>
    public static Control Build(string? html, string? fallbackPlainText = null)
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
            emptyText.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));
            panel.Children.Add(emptyText);
            return panel;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(clean);

        var nodes = doc.DocumentNode.SelectNodes("//body/*") ?? doc.DocumentNode.ChildNodes;
        foreach (var node in nodes.Where(n => n.NodeType == HtmlNodeType.Element))
        {
            panel.Children.Add(BuildBlock(node));
        }
        return panel;
    }

    private static Control BuildBlock(HtmlNode node)
    {
        var text = HtmlEntity.DeEntitize(node.InnerText.Trim());
        switch (node.Name.ToLowerInvariant())
        {
            case "h1": case "h2": case "h3": case "h4":
                return MakeText(text, fontSize: 18, bold: true, spacingFix: true);
            case "li":
                return MakeText("• " + text, fontSize: 14, spacingFix: true);
            case "blockquote":
                return MakeText(text, fontSize: 13, italic: true, spacingFix: true);
            default:
                return MakeText(text, fontSize: 14, spacingFix: true);
        }
    }

    private static TextBlock MakeText(string text, double fontSize, bool bold = false, bool italic = false, bool spacingFix = true)
    {
        var tb = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            FontSize = fontSize,
            FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal,
            FontStyle = italic ? FontStyle.Italic : FontStyle.Normal,
        };
        tb.SetValue(TextBlock.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush"));
        tb.Text = text;
        return tb;
    }
}