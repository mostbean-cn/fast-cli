using System.Text.Json;

namespace FastCli.Desktop.Terminal;

internal sealed class TerminalWebMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string? Type { get; init; }

    public string? Data { get; init; }

    public int? Cols { get; init; }

    public int? Rows { get; init; }

    public static bool TryParse(string json, out TerminalWebMessage message)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<TerminalWebMessage>(json, SerializerOptions);

            if (parsed is null || string.IsNullOrWhiteSpace(parsed.Type))
            {
                message = null!;
                return false;
            }

            switch (parsed.Type)
            {
                case "ready":
                    message = parsed;
                    return true;
                case "input":
                    if (parsed.Data is null || parsed.Data.Length > 65536)
                    {
                        break;
                    }

                    message = parsed;
                    return true;
                case "resize":
                    if (parsed.Cols is > 0 and <= 1000 && parsed.Rows is > 0 and <= 1000)
                    {
                        message = parsed;
                        return true;
                    }

                    break;
            }
        }
        catch
        {
        }

        message = null!;
        return false;
    }
}
