namespace FastCli.Application.Utilities;

public enum TerminalLogKind
{
    Output = 0,
    Error = 1,
    System = 2
}

public readonly record struct TerminalTranscriptLine(TerminalLogKind Kind, string Text);

public static class TerminalTranscriptCodec
{
    private const string OutputPrefix = "[OUT] ";
    private const string ErrorPrefix = "[ERR] ";
    private const string SystemPrefix = "[SYS] ";

    public static string Encode(TerminalLogKind kind, string text)
    {
        return $"{GetPrefix(kind)}{text}";
    }

    public static string AppendLine(string? transcript, TerminalLogKind kind, string text)
    {
        var encodedLine = Encode(kind, text);

        return string.IsNullOrEmpty(transcript)
            ? encodedLine
            : $"{transcript}{Environment.NewLine}{encodedLine}";
    }

    public static IReadOnlyList<TerminalTranscriptLine> DecodeTranscript(string? transcript)
    {
        if (string.IsNullOrEmpty(transcript))
        {
            return [];
        }

        var lines = transcript.Split(["\r\n", "\n"], StringSplitOptions.None);
        var results = new List<TerminalTranscriptLine>(lines.Length);

        foreach (var line in lines)
        {
            results.Add(DecodeLine(line));
        }

        return results;
    }

    public static string ToDisplayText(string? transcript)
    {
        if (string.IsNullOrEmpty(transcript))
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            DecodeTranscript(transcript).Select(static item => item.Text));
    }

    public static TerminalTranscriptLine DecodeLine(string line)
    {
        if (line.StartsWith(OutputPrefix, StringComparison.Ordinal))
        {
            return new TerminalTranscriptLine(TerminalLogKind.Output, line[OutputPrefix.Length..]);
        }

        if (line.StartsWith(ErrorPrefix, StringComparison.Ordinal))
        {
            return new TerminalTranscriptLine(TerminalLogKind.Error, line[ErrorPrefix.Length..]);
        }

        if (line.StartsWith(SystemPrefix, StringComparison.Ordinal))
        {
            return new TerminalTranscriptLine(TerminalLogKind.System, line[SystemPrefix.Length..]);
        }

        return new TerminalTranscriptLine(TerminalLogKind.Output, line);
    }

    private static string GetPrefix(TerminalLogKind kind)
    {
        return kind switch
        {
            TerminalLogKind.Output => OutputPrefix,
            TerminalLogKind.Error => ErrorPrefix,
            TerminalLogKind.System => SystemPrefix,
            _ => OutputPrefix
        };
    }
}
