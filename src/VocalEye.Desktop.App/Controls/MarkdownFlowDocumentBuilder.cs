using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using MdBlock = Markdig.Syntax.Block;
using MdInline = Markdig.Syntax.Inlines.Inline;
using WpfInline = System.Windows.Documents.Inline;

namespace VocalEye.Desktop.App.Controls;

public sealed record MarkdownTheme(Brush Foreground, Brush CodeBackground, Brush CodeForeground, Brush LinkForeground, Brush QuoteBar);

/// <summary>
/// Converts Markdown (parsed by Markdig) into a WPF <see cref="FlowDocument"/> for the Phase 2 subset:
/// headings, paragraphs, bold/italic, lists, inline code, fenced code, quotes, links.
/// Images are shown as their alt text (nothing is fetched). Raw HTML is shown as plain text.
/// </summary>
public static class MarkdownFlowDocumentBuilder
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseEmphasisExtras()
        .UseAutoLinks()
        .Build();

    private static readonly FontFamily Mono = new("Cascadia Mono, Consolas, Courier New");

    public static FlowDocument Build(string? markdown, MarkdownTheme theme)
    {
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(0),
            Foreground = theme.Foreground,
            Background = Brushes.Transparent,
            ColumnWidth = double.PositiveInfinity,
            TextAlignment = TextAlignment.Left
        };

        if (string.IsNullOrEmpty(markdown)) return doc;

        var parsed = Markdown.Parse(markdown, Pipeline);
        foreach (var block in parsed) AddBlock(doc.Blocks, block, theme);
        return doc;
    }

    private static void AddBlock(BlockCollection target, MdBlock block, MarkdownTheme theme)
    {
        switch (block)
        {
            case HeadingBlock h:
            {
                var p = new Paragraph { FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
                p.FontSize = h.Level switch { 1 => 24, 2 => 20, 3 => 17, _ => 15 };
                AddInlines(p.Inlines, h.Inline, theme);
                target.Add(p);
                break;
            }
            case ParagraphBlock pb:
            {
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                AddInlines(p.Inlines, pb.Inline, theme);
                target.Add(p);
                break;
            }
            case ListBlock lb:
            {
                var list = new List
                {
                    MarkerStyle = lb.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc,
                    Margin = new Thickness(0, 0, 0, 8),
                    Padding = new Thickness(22, 0, 0, 0)
                };
                foreach (var item in lb.OfType<ListItemBlock>())
                {
                    var li = new ListItem();
                    foreach (var child in item) AddBlock(li.Blocks, child, theme);
                    foreach (var p in li.Blocks.OfType<Paragraph>()) p.Margin = new Thickness(0, 0, 0, 2);
                    list.ListItems.Add(li);
                }
                target.Add(list);
                break;
            }
            case CodeBlock cb: // includes FencedCodeBlock
            {
                var code = cb.Lines.ToString().TrimEnd('\r', '\n');
                var p = new Paragraph(new Run(code))
                {
                    FontFamily = Mono,
                    FontSize = 13,
                    Foreground = theme.CodeForeground,
                    Background = theme.CodeBackground,
                    Padding = new Thickness(12, 10, 12, 10),
                    Margin = new Thickness(0, 4, 0, 10)
                };
                target.Add(p);
                break;
            }
            case QuoteBlock qb:
            {
                var section = new Section
                {
                    BorderBrush = theme.QuoteBar,
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(12, 0, 0, 0),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                foreach (var child in qb) AddBlock(section.Blocks, child, theme);
                target.Add(section);
                break;
            }
            case ThematicBreakBlock:
                target.Add(new BlockUIContainer(new System.Windows.Controls.Separator { Margin = new Thickness(0, 6, 0, 10) }));
                break;
            case LeafBlock leaf: // HtmlBlock and anything else: show as text
            {
                var text = leaf.Inline is not null ? null : leaf.Lines.ToString();
                var p = new Paragraph { Margin = new Thickness(0, 0, 0, 8) };
                if (leaf.Inline is not null) AddInlines(p.Inlines, leaf.Inline, theme);
                else p.Inlines.Add(new Run(text));
                target.Add(p);
                break;
            }
            case ContainerBlock container:
                foreach (var child in container) AddBlock(target, child, theme);
                break;
        }
    }

    private static void AddInlines(InlineCollection target, ContainerInline? container, MarkdownTheme theme)
    {
        if (container is null) return;
        foreach (var inline in container) AddInline(target, inline, theme);
    }

    private static void AddInline(InlineCollection target, MdInline inline, MarkdownTheme theme)
    {
        switch (inline)
        {
            case LiteralInline lit:
                target.Add(new Run(lit.Content.ToString()));
                break;

            case CodeInline code:
                target.Add(new Run(code.Content)
                {
                    FontFamily = Mono,
                    Foreground = theme.CodeForeground,
                    Background = theme.CodeBackground
                });
                break;

            case LineBreakInline lb:
                if (lb.IsHard) target.Add(new LineBreak()); else target.Add(new Run(" "));
                break;

            case EmphasisInline em:
            {
                Span span = em.DelimiterChar == '~' ? new Span { TextDecorations = TextDecorations.Strikethrough }
                    : em.DelimiterCount >= 2 ? new Bold()
                    : new Italic();
                AddInlines(span.Inlines, em, theme);
                target.Add(span);
                break;
            }

            case LinkInline link when link.IsImage:
                // Never fetch remote images; show the alt text instead.
                AddInlines(target, link, theme);
                break;

            case LinkInline link:
            {
                var run = new Span();
                AddInlines(run.Inlines, link, theme);
                if (TryGetSafeUri(link.Url, out var uri))
                {
                    var hyperlink = new Hyperlink { NavigateUri = uri, Foreground = theme.LinkForeground, ToolTip = uri.ToString() };
                    hyperlink.Inlines.Add(run);
                    target.Add(hyperlink);
                }
                else target.Add(run); // unsafe/relative link: plain text, not clickable
                break;
            }

            case AutolinkInline auto:
            {
                if (TryGetSafeUri(auto.Url, out var uri))
                {
                    var hyperlink = new Hyperlink(new Run(auto.Url)) { NavigateUri = uri, Foreground = theme.LinkForeground };
                    target.Add(hyperlink);
                }
                else target.Add(new Run(auto.Url));
                break;
            }

            case HtmlInline html:
                target.Add(new Run(html.Tag));
                break;

            case ContainerInline container:
                AddInlines(target, container, theme);
                break;
        }
    }

    /// <summary>Link policy: only absolute http, https and mailto URLs are clickable.</summary>
    public static bool TryGetSafeUri(string? url, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme is not ("http" or "https" or "mailto")) return false;
        uri = parsed;
        return true;
    }
}
