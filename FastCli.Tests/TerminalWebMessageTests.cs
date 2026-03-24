using FastCli.Desktop.Terminal;
using Xunit;

namespace FastCli.Tests;

public sealed class TerminalWebMessageTests
{
    [Fact]
    public void TryParse_AcceptsLowercaseWebViewMessagePayload()
    {
        const string payload = "{\"type\":\"input\",\"data\":\"dir\\r\"}";

        var parsed = TerminalWebMessage.TryParse(payload, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("input", message.Type);
        Assert.Equal("dir\r", message.Data);
    }

    [Fact]
    public void TryParse_AcceptsResizePayload()
    {
        const string payload = "{\"type\":\"resize\",\"cols\":120,\"rows\":40}";

        var parsed = TerminalWebMessage.TryParse(payload, out var message);

        Assert.True(parsed);
        Assert.NotNull(message);
        Assert.Equal("resize", message.Type);
        Assert.Equal(120, message.Cols);
        Assert.Equal(40, message.Rows);
    }

    [Fact]
    public void TryParse_RejectsZeroSizedResizePayload()
    {
        const string payload = "{\"type\":\"resize\",\"cols\":0,\"rows\":40}";

        var parsed = TerminalWebMessage.TryParse(payload, out var message);

        Assert.False(parsed);
        Assert.Null(message);
    }
}
