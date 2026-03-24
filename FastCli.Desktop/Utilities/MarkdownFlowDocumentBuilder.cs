using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace FastCli.Desktop.Utilities;

public static class MarkdownFlowDocumentBuilder
{
    private static readonly Regex HeadingPattern = new(@"^(#{1,6})\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex UnorderedListPattern = new(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
    private static readonly Regex OrderedListPattern = new(@"^\s*\d+\.\s+(.*)$", RegexOptions.Compiled);

    public static FlowDocument Build(string? markdown)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            TextAlignment = TextAlignment.Left
        };

        if (string.IsNullOrWhiteSpace(markdown))
        {
            document.Blocks.Add(CreateParagraph(string.Empty));
            return document;
        }

        var normalized = markdown.Replace("\r\n", "\n");
        var lines = normalized.Split('\n');
        var paragraphLines = new List<string>();
        var listItems = new List<string>();
        TextMarkerStyle? currentListMarker = null;
        var codeLines = new List<string>();
        var inCodeBlock = false;

        void FlushParagraph()
        {
            if (paragraphLines.Count == 0)
            {
                return;
            }

            document.Blocks.Add(CreateParagraph(string.Join(" ", paragraphLines).Trim()));
            paragraphLines.Clear();
        }

        void FlushList()
        {
            if (listItems.Count == 0 || currentListMarker is null)
            {
                return;
            }

            var list = new System.Windows.Documents.List
            {
                MarkerStyle = currentListMarker.Value,
                Margin = new Thickness(0, 0, 0, 12)
            };

            foreach (var item in listItems)
            {
                list.ListItems.Add(new ListItem(CreateParagraph(item.Trim())));
            }

            document.Blocks.Add(list);
            listItems.Clear();
            currentListMarker = null;
        }

        void FlushCodeBlock()
        {
            if (codeLines.Count == 0)
            {
                return;
            }

            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10, 8, 10, 8),
                FontFamily = new FontFamily("Cascadia Mono, Consolas"),
                Background = GetBrush("ControlBackgroundBrush", Color.FromRgb(32, 35, 42)),
                BorderBrush = GetBrush("ControlBorderBrush", Color.FromRgb(55, 65, 81)),
                BorderThickness = new Thickness(1)
            };
            paragraph.Inlines.Add(new Run(string.Join(Environment.NewLine, codeLines)));
            document.Blocks.Add(paragraph);
            codeLines.Clear();
        }

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph();
                FlushList();

                if (inCodeBlock)
                {
                    FlushCodeBlock();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                    codeLines.Clear();
                }

                continue;
            }

            if (inCodeBlock)
            {
                codeLines.Add(line);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph();
                FlushList();
                continue;
            }

            var headingMatch = HeadingPattern.Match(line);
            if (headingMatch.Success)
            {
                FlushParagraph();
                FlushList();
                document.Blocks.Add(CreateHeading(headingMatch.Groups[2].Value.Trim(), headingMatch.Groups[1].Value.Length));
                continue;
            }

            var unorderedMatch = UnorderedListPattern.Match(line);
            if (unorderedMatch.Success)
            {
                FlushParagraph();
                if (currentListMarker is not null && currentListMarker != TextMarkerStyle.Disc)
                {
                    FlushList();
                }

                currentListMarker = TextMarkerStyle.Disc;
                listItems.Add(unorderedMatch.Groups[1].Value);
                continue;
            }

            var orderedMatch = OrderedListPattern.Match(line);
            if (orderedMatch.Success)
            {
                FlushParagraph();
                if (currentListMarker is not null && currentListMarker != TextMarkerStyle.Decimal)
                {
                    FlushList();
                }

                currentListMarker = TextMarkerStyle.Decimal;
                listItems.Add(orderedMatch.Groups[1].Value);
                continue;
            }

            FlushList();
            paragraphLines.Add(line.Trim());
        }

        if (inCodeBlock)
        {
            FlushCodeBlock();
        }
        else
        {
            FlushParagraph();
        }

        FlushList();

        if (document.Blocks.Count == 0)
        {
            document.Blocks.Add(CreateParagraph(string.Empty));
        }

        return document;
    }

    private static Paragraph CreateHeading(string text, int level)
    {
        var fontSize = level switch
        {
            1 => 21d,
            2 => 18d,
            3 => 16d,
            _ => 14d
        };

        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10),
            FontWeight = FontWeights.SemiBold,
            FontSize = fontSize
        };
        AddInlineElements(paragraph.Inlines, text);
        return paragraph;
    }

    private static Paragraph CreateParagraph(string text)
    {
        var paragraph = new Paragraph
        {
            Margin = new Thickness(0, 0, 0, 10)
        };
        AddInlineElements(paragraph.Inlines, text);
        return paragraph;
    }

    private static void AddInlineElements(InlineCollection inlines, string text)
    {
        foreach (var inline in ParseInlines(text))
        {
            inlines.Add(inline);
        }
    }

    private static IEnumerable<Inline> ParseInlines(string text)
    {
        var plainText = new StringBuilder();
        var index = 0;
        var inlineParts = new List<Inline>();

        void FlushPlainText()
        {
            if (plainText.Length == 0)
            {
                return;
            }

            inlineParts.Add(new Run(plainText.ToString()));
            plainText.Clear();
        }

        while (index < text.Length)
        {
            if (TryParseLink(text, index, out var linkInline, out var consumed)
                || TryParseBold(text, index, out linkInline, out consumed)
                || TryParseItalic(text, index, out linkInline, out consumed)
                || TryParseCode(text, index, out linkInline, out consumed))
            {
                FlushPlainText();
                inlineParts.Add(linkInline);
                index += consumed;
                continue;
            }

            plainText.Append(text[index]);
            index++;
        }

        FlushPlainText();
        return inlineParts;
    }

    private static bool TryParseLink(string text, int startIndex, out Inline inline, out int consumed)
    {
        inline = null!;
        consumed = 0;

        if (text[startIndex] != '[')
        {
            return false;
        }

        var labelEnd = text.IndexOf(']', startIndex + 1);
        if (labelEnd < 0 || labelEnd + 1 >= text.Length || text[labelEnd + 1] != '(')
        {
            return false;
        }

        var uriEnd = text.IndexOf(')', labelEnd + 2);
        if (uriEnd < 0)
        {
            return false;
        }

        var label = text[(startIndex + 1)..labelEnd];
        var uriText = text[(labelEnd + 2)..uriEnd];
        if (!Uri.TryCreate(uriText, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var hyperlink = new Hyperlink
        {
            NavigateUri = uri
        };
        hyperlink.Click += Hyperlink_Click;
        AddInlineElements(hyperlink.Inlines, label);

        inline = hyperlink;
        consumed = uriEnd - startIndex + 1;
        return true;
    }

    private static bool TryParseBold(string text, int startIndex, out Inline inline, out int consumed)
    {
        inline = null!;
        consumed = 0;

        if (!text.AsSpan(startIndex).StartsWith("**"))
        {
            return false;
        }

        var endIndex = text.IndexOf("**", startIndex + 2, StringComparison.Ordinal);
        if (endIndex < 0)
        {
            return false;
        }

        var span = new Span
        {
            FontWeight = FontWeights.SemiBold
        };
        AddInlineElements(span.Inlines, text[(startIndex + 2)..endIndex]);

        inline = span;
        consumed = endIndex - startIndex + 2;
        return true;
    }

    private static bool TryParseItalic(string text, int startIndex, out Inline inline, out int consumed)
    {
        inline = null!;
        consumed = 0;

        if (text[startIndex] != '*' || text.AsSpan(startIndex).StartsWith("**"))
        {
            return false;
        }

        var endIndex = text.IndexOf('*', startIndex + 1);
        if (endIndex < 0)
        {
            return false;
        }

        var span = new Span
        {
            FontStyle = FontStyles.Italic
        };
        AddInlineElements(span.Inlines, text[(startIndex + 1)..endIndex]);

        inline = span;
        consumed = endIndex - startIndex + 1;
        return true;
    }

    private static bool TryParseCode(string text, int startIndex, out Inline inline, out int consumed)
    {
        inline = null!;
        consumed = 0;

        if (text[startIndex] != '`')
        {
            return false;
        }

        var endIndex = text.IndexOf('`', startIndex + 1);
        if (endIndex < 0)
        {
            return false;
        }

        inline = new Run(text[(startIndex + 1)..endIndex])
        {
            FontFamily = new FontFamily("Cascadia Mono, Consolas"),
            Background = GetBrush("SelectedItemBackgroundBrush", Color.FromRgb(40, 44, 52))
        };
        consumed = endIndex - startIndex + 1;
        return true;
    }

    private static void Hyperlink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Hyperlink { NavigateUri: not null } hyperlink)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = hyperlink.NavigateUri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch
        {
        }
    }

    private static Brush GetBrush(string resourceKey, Color fallbackColor)
    {
        return System.Windows.Application.Current?.TryFindResource(resourceKey) as Brush
            ?? new SolidColorBrush(fallbackColor);
    }
}
