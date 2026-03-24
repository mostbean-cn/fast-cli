namespace FastCli.Application.Models;

public sealed class AnsiTextSegment
{
    public required string Text { get; init; }
    public AnsiColorInfo? Foreground { get; init; }
    public AnsiColorInfo? Background { get; init; }
    public AnsiTextDecoration Decoration { get; init; }
}

public sealed class AnsiColorInfo
{
    public AnsiColorKind Kind { get; init; }
    public byte R { get; init; }
    public byte G { get; init; }
    public byte B { get; init; }
    public byte Index { get; init; }

    private static readonly (byte R, byte G, byte B)[] StandardColors =
    [
        ((byte)0, (byte)0, (byte)0),       // Black
        ((byte)170, (byte)0, (byte)0),     // Red
        ((byte)0, (byte)170, (byte)0),     // Green
        ((byte)170, (byte)170, (byte)0),   // Yellow
        ((byte)0, (byte)0, (byte)170),     // Blue
        ((byte)170, (byte)0, (byte)170),   // Magenta
        ((byte)0, (byte)170, (byte)170),   // Cyan
        ((byte)170, (byte)170, (byte)170), // White
        ((byte)85, (byte)85, (byte)85),    // Bright Black
        ((byte)255, (byte)85, (byte)85),   // Bright Red
        ((byte)85, (byte)255, (byte)85),   // Bright Green
        ((byte)255, (byte)255, (byte)85),  // Bright Yellow
        ((byte)85, (byte)85, (byte)255),   // Bright Blue
        ((byte)255, (byte)85, (byte)255),  // Bright Magenta
        ((byte)85, (byte)255, (byte)255),  // Bright Cyan
        ((byte)255, (byte)255, (byte)255)  // Bright White
    ];

    public static AnsiColorInfo FromStandardIndex(byte index)
    {
        var (r, g, b) = index < StandardColors.Length
            ? StandardColors[index]
            : ((byte)170, (byte)170, (byte)170);

        return new AnsiColorInfo
        {
            Kind = AnsiColorKind.Standard,
            Index = index,
            R = r,
            G = g,
            B = b
        };
    }

    public static AnsiColorInfo FromExtendedIndex(byte index)
    {
        byte r, g, b;

        if (index < 16)
        {
            return FromStandardIndex(index);
        }
        else if (index < 232)
        {
            var i = index - 16;
            r = (byte)((i / 36) * 51);
            g = (byte)(((i % 36) / 6) * 51);
            b = (byte)((i % 6) * 51);
        }
        else
        {
            var gray = (byte)((index - 232) * 10 + 8);
            r = g = b = gray;
        }

        return new AnsiColorInfo
        {
            Kind = AnsiColorKind.Extended,
            Index = index,
            R = r,
            G = g,
            B = b
        };
    }

    public static AnsiColorInfo FromRgb(byte r, byte g, byte b)
    {
        return new AnsiColorInfo
        {
            Kind = AnsiColorKind.Rgb,
            R = r,
            G = g,
            B = b
        };
    }

    public string ToHex()
    {
        return $"#{R:X2}{G:X2}{B:X2}";
    }
}

public enum AnsiColorKind
{
    Default,
    Standard,
    Extended,
    Rgb
}

[Flags]
public enum AnsiTextDecoration
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Blink = 16,
    Reverse = 32,
    Hidden = 64,
    Strikethrough = 128
}
