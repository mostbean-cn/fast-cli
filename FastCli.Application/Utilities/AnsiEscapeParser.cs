using System.Text.RegularExpressions;
using FastCli.Application.Models;

namespace FastCli.Application.Utilities;

public sealed partial class AnsiEscapeParser
{
    private static readonly Regex AnsiPattern = CreateAnsiPattern();

    [GeneratedRegex(@"\x1b\[[0-9;]*[a-zA-Z]|\x1b\[\?[0-9;]*[a-zA-Z]", RegexOptions.Compiled)]
    private static partial Regex CreateAnsiPattern();

    public IReadOnlyList<AnsiTextSegment> Parse(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return [new AnsiTextSegment { Text = string.Empty }];
        }

        var segments = new List<AnsiTextSegment>();
        AnsiColorInfo? currentFg = null;
        AnsiColorInfo? currentBg = null;
        var currentDecoration = AnsiTextDecoration.None;

        var lastIndex = 0;
        var matches = AnsiPattern.Matches(input);

        foreach (Match match in matches)
        {
            if (match.Index > lastIndex)
            {
                segments.Add(CreateSegment(
                    input[lastIndex..match.Index],
                    currentFg,
                    currentBg,
                    currentDecoration));
            }

            var codes = match.Groups[1].Value.Split(';', StringSplitOptions.RemoveEmptyEntries);
            ParseSgrCodes(codes, ref currentFg, ref currentBg, ref currentDecoration);

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < input.Length)
        {
            segments.Add(CreateSegment(
                input[lastIndex..],
                currentFg,
                currentBg,
                currentDecoration));
        }

        return segments.Count > 0 ? segments : [new AnsiTextSegment { Text = string.Empty }];
    }

    public static string StripAnsi(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        return AnsiPattern.Replace(input, string.Empty);
    }

    private static AnsiTextSegment CreateSegment(
        string text,
        AnsiColorInfo? fg,
        AnsiColorInfo? bg,
        AnsiTextDecoration decoration)
    {
        return new AnsiTextSegment
        {
            Text = text,
            Foreground = fg,
            Background = bg,
            Decoration = decoration
        };
    }

    private static void ParseSgrCodes(
        string[] codes,
        ref AnsiColorInfo? fg,
        ref AnsiColorInfo? bg,
        ref AnsiTextDecoration decoration)
    {
        for (var i = 0; i < codes.Length; i++)
        {
            if (!int.TryParse(codes[i], out var code))
            {
                continue;
            }

            switch (code)
            {
                case 0: // Reset
                    fg = null;
                    bg = null;
                    decoration = AnsiTextDecoration.None;
                    break;
                case 1: // Bold
                    decoration |= AnsiTextDecoration.Bold;
                    break;
                case 2: // Dim
                    decoration |= AnsiTextDecoration.Dim;
                    break;
                case 3: // Italic
                    decoration |= AnsiTextDecoration.Italic;
                    break;
                case 4: // Underline
                    decoration |= AnsiTextDecoration.Underline;
                    break;
                case 5: // Blink
                    decoration |= AnsiTextDecoration.Blink;
                    break;
                case 7: // Reverse
                    decoration |= AnsiTextDecoration.Reverse;
                    break;
                case 8: // Hidden
                    decoration |= AnsiTextDecoration.Hidden;
                    break;
                case 9: // Strikethrough
                    decoration |= AnsiTextDecoration.Strikethrough;
                    break;
                case 21: // Bold off
                    decoration &= ~AnsiTextDecoration.Bold;
                    break;
                case 22: // Dim off
                    decoration &= ~(AnsiTextDecoration.Bold | AnsiTextDecoration.Dim);
                    break;
                case 23: // Italic off
                    decoration &= ~AnsiTextDecoration.Italic;
                    break;
                case 24: // Underline off
                    decoration &= ~AnsiTextDecoration.Underline;
                    break;
                case 25: // Blink off
                    decoration &= ~AnsiTextDecoration.Blink;
                    break;
                case 27: // Reverse off
                    decoration &= ~AnsiTextDecoration.Reverse;
                    break;
                case 28: // Hidden off
                    decoration &= ~AnsiTextDecoration.Hidden;
                    break;
                case 29: // Strikethrough off
                    decoration &= ~AnsiTextDecoration.Strikethrough;
                    break;
                case 39: // Default foreground
                    fg = null;
                    break;
                case 49: // Default background
                    bg = null;
                    break;
                case >= 30 and <= 37: // Standard foreground
                    fg = AnsiColorInfo.FromStandardIndex((byte)(code - 30));
                    break;
                case >= 40 and <= 47: // Standard background
                    bg = AnsiColorInfo.FromStandardIndex((byte)(code - 40));
                    break;
                case >= 90 and <= 97: // Bright foreground
                    fg = AnsiColorInfo.FromStandardIndex((byte)(code - 82));
                    break;
                case >= 100 and <= 107: // Bright background
                    bg = AnsiColorInfo.FromStandardIndex((byte)(code - 92));
                    break;
                case 38: // Extended foreground
                    if (i + 1 < codes.Length && int.TryParse(codes[i + 1], out var fgMode))
                    {
                        if (fgMode == 5 && i + 2 < codes.Length && byte.TryParse(codes[i + 2], out var fgIndex))
                        {
                            fg = AnsiColorInfo.FromExtendedIndex(fgIndex);
                            i += 2;
                        }
                        else if (fgMode == 2 && i + 4 < codes.Length)
                        {
                            if (byte.TryParse(codes[i + 2], out var r) &&
                                byte.TryParse(codes[i + 3], out var g) &&
                                byte.TryParse(codes[i + 4], out var b))
                            {
                                fg = AnsiColorInfo.FromRgb(r, g, b);
                                i += 4;
                            }
                        }
                    }
                    break;
                case 48: // Extended background
                    if (i + 1 < codes.Length && int.TryParse(codes[i + 1], out var bgMode))
                    {
                        if (bgMode == 5 && i + 2 < codes.Length && byte.TryParse(codes[i + 2], out var bgIndex))
                        {
                            bg = AnsiColorInfo.FromExtendedIndex(bgIndex);
                            i += 2;
                        }
                        else if (bgMode == 2 && i + 4 < codes.Length)
                        {
                            if (byte.TryParse(codes[i + 2], out var r) &&
                                byte.TryParse(codes[i + 3], out var g) &&
                                byte.TryParse(codes[i + 4], out var b))
                            {
                                bg = AnsiColorInfo.FromRgb(r, g, b);
                                i += 4;
                            }
                        }
                    }
                    break;
            }
        }
    }
}
