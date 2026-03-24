using System.Text;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;
using FastCli.Infrastructure.Execution;
using Xunit;

namespace FastCli.Tests;

public sealed class ConPtyCommandExecutorTests
{
    [Fact]
    public async Task StartEmbeddedAsync_SupportsInteractiveShellInputAndStreamingOutput()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var executor = new ConPtyCommandExecutor(new TestLocalizer());
        var output = new StringBuilder();
        var outputLock = new object();

        var session = await executor.StartEmbeddedAsync(
            new CommandExecutionRequest
            {
                Name = "cmd-shell",
                ShellType = ShellType.Direct,
                RunMode = CommandRunMode.Embedded,
                CommandText = "cmd.exe",
                Arguments = ["/Q"]
            },
            line =>
            {
                lock (outputLock)
                {
                    output.Append(line.Text);
                }
            });

        await WaitUntilAsync(
            () =>
            {
                lock (outputLock)
                {
                    return output.Length > 0;
                }
            },
            TimeSpan.FromSeconds(10));

        await session.SendInputAsync!(Encoding.UTF8.GetBytes("echo fastcli-pty\r"), CancellationToken.None);

        await WaitUntilAsync(
            () =>
            {
                lock (outputLock)
                {
                    return output.ToString().Contains("fastcli-pty", StringComparison.OrdinalIgnoreCase);
                }
            },
            TimeSpan.FromSeconds(10));

        await session.SendInputAsync!(Encoding.UTF8.GetBytes("exit /b 0\r"), CancellationToken.None);
        var completion = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        var terminalText = output.ToString();
        Assert.Equal(ExecutionStatus.Success, completion.Status);
        Assert.Contains("fastcli-pty", terminalText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("[SYS]", terminalText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartEmbeddedAsync_WithCmdShellCommand_CompletesAfterConfiguredCommandRuns()
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var executor = new ConPtyCommandExecutor(new TestLocalizer());
        var output = new StringBuilder();
        var outputLock = new object();

        var session = await executor.StartEmbeddedAsync(
            new CommandExecutionRequest
            {
                Name = "cmd-command",
                ShellType = ShellType.Cmd,
                RunMode = CommandRunMode.Embedded,
                CommandText = "echo seeded"
            },
            line =>
            {
                lock (outputLock)
                {
                    output.Append(line.Text);
                }
            });

        await WaitUntilAsync(
            () =>
            {
                lock (outputLock)
                {
                    return output.ToString().Contains("seeded", StringComparison.OrdinalIgnoreCase);
                }
            },
            TimeSpan.FromSeconds(10));

        var completion = await session.Completion.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.NotEqual(ExecutionStatus.Canceled, completion.Status);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var startedAt = DateTime.UtcNow;

        while (!condition())
        {
            if (DateTime.UtcNow - startedAt > timeout)
            {
                throw new TimeoutException("Condition was not met within the expected time.");
            }

            await Task.Delay(50);
        }
    }

    private sealed class TestLocalizer : IAppLocalizer
    {
        public string Get(string key) => key;

        public string Format(string key, params object?[] args) => $"{key}:{string.Join(", ", args)}";
    }
}
