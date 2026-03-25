using System.Reflection;
using System.Text;
using EasyWindowsTerminalControl;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;
using FastCli.Infrastructure.Execution;

namespace FastCli.Desktop.Terminal;

public sealed class WindowsTerminalCommandExecutor : ICommandExecutor
{
    private const int DefaultColumns = 120;
    private const int DefaultRows = 30;

    private readonly IAppLocalizer _localizer;

    public WindowsTerminalCommandExecutor(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request)
    {
        return ShellCommandFactory.BuildDisplayInfo(request);
    }

    public Task<CommandSession> StartEmbeddedAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        return StartInteractiveAsync(request, onOutput, cancellationToken);
    }

    public Task<CommandCompletionResult> StartExternalAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var fallback = new ProcessCommandExecutor(_localizer);
        return fallback.StartExternalAsync(request, cancellationToken);
    }

    public Task<CommandSession> StartTerminalAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        return StartInteractiveAsync(request, onOutput, cancellationToken);
    }

    private Task<CommandSession> StartInteractiveAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.RunAsAdministrator)
        {
            throw new InvalidOperationException(_localizer.Get("Service_EmbeddedAdminNotSupported"));
        }

        var term = new ForwardingTermPty(onOutput);
        var completionSource = new TaskCompletionSource<CommandCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;
        var launch = BuildLaunchCommand(request);
        var registration = cancellationToken.Register(() =>
        {
            _ = StopTermAsync(term, () => Interlocked.Exchange(ref canceled, 1));
        });

        _ = Task.Run(
            () =>
            {
                try
                {
                    term.Start(launch.CommandLine, DefaultColumns, DefaultRows, logOutput: false);
                    completionSource.TrySetResult(CreateCompletionResult(term, Interlocked.CompareExchange(ref canceled, 0, 0) == 1));
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
                finally
                {
                    registration.Dispose();
                }
            },
            CancellationToken.None);

        Func<byte[], CancellationToken, Task> sendInput = (bytes, _) =>
        {
            if (bytes.Length == 0 || completionSource.Task.IsCompleted)
            {
                return Task.CompletedTask;
            }

            try
            {
                var text = Encoding.UTF8.GetString(bytes);
                term.WriteToTerm(text.AsSpan());
            }
            catch
            {
            }

            return Task.CompletedTask;
        };

        Func<int, int, CancellationToken, Task> resizeAsync = (cols, rows, _) =>
        {
            if (cols <= 0 || rows <= 0 || completionSource.Task.IsCompleted)
            {
                return Task.CompletedTask;
            }

            try
            {
                term.Resize(cols, rows);
            }
            catch
            {
            }

            return Task.CompletedTask;
        };

        return Task.FromResult(new CommandSession(
            request.ExecutionId,
            completionSource.Task,
            _ => StopTermAsync(term, () => Interlocked.Exchange(ref canceled, 1)))
        {
            SendInputAsync = sendInput,
            ResizeAsync = resizeAsync,
            NativeTerminalHandle = term
        });
    }

    private CommandCompletionResult CreateCompletionResult(TermPTY term, bool canceled)
    {
        var exitCode = TryReadExitCode(term);

        if (canceled)
        {
            return new CommandCompletionResult
            {
                Status = ExecutionStatus.Canceled,
                ExitCode = exitCode,
                Summary = _localizer.Get("Service_CommandWasCanceled")
            };
        }

        if (exitCode is null or 0)
        {
            return new CommandCompletionResult
            {
                Status = ExecutionStatus.Success,
                ExitCode = exitCode,
                Summary = _localizer.Get("Service_CommandSucceeded")
            };
        }

        return new CommandCompletionResult
        {
            Status = ExecutionStatus.Failure,
            ExitCode = exitCode,
            Summary = _localizer.Format("Service_CommandFailedWithExitCode", exitCode.Value)
        };
    }

    private static async Task StopTermAsync(TermPTY term, Action markCanceled)
    {
        try
        {
            markCanceled();
            term.CloseStdinToApp();
            term.StopExternalTermOnly();
            await Task.Delay(50).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static int? TryReadExitCode(TermPTY term)
    {
        try
        {
            var processWrapper = term.Process;

            if (processWrapper is null)
            {
                return null;
            }

            var exitCodeProperty = processWrapper.GetType().GetProperty(
                "ExitCode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (exitCodeProperty?.GetValue(processWrapper) is int exitCode)
            {
                return exitCode;
            }

            var processProperty = processWrapper.GetType().GetProperty(
                "Process",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            var processInstance = processProperty?.GetValue(processWrapper);
            var directExitCode = processInstance?.GetType().GetProperty(
                "ExitCode",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (directExitCode?.GetValue(processInstance) is int reflectedExitCode)
            {
                return reflectedExitCode;
            }
        }
        catch
        {
        }

        return null;
    }

    private LaunchCommand BuildLaunchCommand(CommandExecutionRequest request)
    {
        return request.ShellType switch
        {
            ShellType.Cmd => BuildCmdLaunchCommand(request),
            ShellType.PowerShell => BuildPowerShellLaunchCommand("powershell.exe", request),
            ShellType.Pwsh => BuildPowerShellLaunchCommand("pwsh.exe", request),
            ShellType.Direct => BuildDirectLaunchCommand(request),
            _ => throw new InvalidOperationException(_localizer.Get("Service_UnsupportedShellType"))
        };
    }

    private LaunchCommand BuildCmdLaunchCommand(CommandExecutionRequest request)
    {
        var executable = ShellCommandFactory.ResolveShellPath(ShellType.Cmd);
        var hasPayload = !string.IsNullOrWhiteSpace(request.CommandText);
        var bootstrap = BuildCmdBootstrap(request, hasPayload);

        if (string.IsNullOrWhiteSpace(bootstrap))
        {
            return new LaunchCommand(BuildCommandLine(executable, []));
        }

        var arguments = hasPayload
            ? new[] { "/C", bootstrap }
            : new[] { "/K", bootstrap };

        return new LaunchCommand(BuildCommandLine(executable, arguments));
    }

    private LaunchCommand BuildDirectLaunchCommand(CommandExecutionRequest request)
    {
        var bootstrap = BuildCmdBootstrap(request, hasPayload: true);
        return new LaunchCommand(BuildCommandLine("cmd.exe", ["/C", bootstrap]));
    }

    private LaunchCommand BuildPowerShellLaunchCommand(string executable, CommandExecutionRequest request)
    {
        var hasPayload = !string.IsNullOrWhiteSpace(request.CommandText);
        var script = BuildPowerShellBootstrap(request, hasPayload);
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var arguments = hasPayload
            ? new[] { "-NoLogo", "-NoProfile", "-EncodedCommand", encoded }
            : new[] { "-NoLogo", "-NoProfile", "-NoExit", "-EncodedCommand", encoded };

        return new LaunchCommand(BuildCommandLine(executable, arguments));
    }

    private string BuildCmdBootstrap(CommandExecutionRequest request, bool hasPayload)
    {
        var segments = new List<string>();

        foreach (var entry in BuildTerminalEnvironment(request))
        {
            segments.Add($"set \"{entry.Key}={EscapeCmdSetValue(entry.Value)}\"");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            segments.Add($"cd /d {QuoteWindowsArgument(ShellCommandFactory.ResolveWorkingDirectory(request.WorkingDirectory))}");
        }

        if (hasPayload)
        {
            segments.Add(BuildCommandPayload(request));
        }

        return string.Join(" && ", segments);
    }

    private string BuildPowerShellBootstrap(CommandExecutionRequest request, bool hasPayload)
    {
        var script = new StringBuilder();
        script.AppendLine("[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)");
        script.AppendLine("[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)");
        script.AppendLine("$OutputEncoding = [Console]::OutputEncoding");
        script.AppendLine("if ($PSVersionTable.PSVersion.Major -ge 7) { $PSStyle.OutputRendering = 'PlainText' }");

        foreach (var entry in BuildTerminalEnvironment(request))
        {
            script.AppendLine($"[System.Environment]::SetEnvironmentVariable('{EscapePowerShellString(entry.Key)}', '{EscapePowerShellString(entry.Value)}', 'Process')");
        }

        if (!string.IsNullOrWhiteSpace(request.WorkingDirectory))
        {
            script.AppendLine($"Set-Location -LiteralPath '{EscapePowerShellString(ShellCommandFactory.ResolveWorkingDirectory(request.WorkingDirectory))}'");
        }

        if (hasPayload)
        {
            script.AppendLine(BuildCommandPayload(request));
        }

        return script.ToString();
    }

    private static IReadOnlyDictionary<string, string> BuildTerminalEnvironment(CommandExecutionRequest request)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor"
        };

        foreach (var item in request.EnvironmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                variables[item.Key] = item.Value ?? string.Empty;
            }
        }

        return variables;
    }

    private static string BuildCommandPayload(CommandExecutionRequest request)
    {
        if (request.ShellType == ShellType.Direct)
        {
            return BuildCommandLine(request.CommandText, request.Arguments);
        }

        var builder = new StringBuilder(request.CommandText.Trim());

        foreach (var argument in request.Arguments.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            builder.Append(' ');
            builder.Append(QuoteWindowsArgument(argument));
        }

        return builder.ToString().Trim();
    }

    private static string BuildCommandLine(string executable, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append(QuoteWindowsArgument(executable));

        foreach (var argument in arguments)
        {
            builder.Append(' ');
            builder.Append(QuoteWindowsArgument(argument));
        }

        return builder.ToString();
    }

    private static string QuoteWindowsArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(static ch => char.IsWhiteSpace(ch) || ch is '"' or '\\'))
        {
            return value;
        }

        var builder = new StringBuilder();
        builder.Append('"');
        var backslashCount = 0;

        foreach (var ch in value)
        {
            if (ch == '\\')
            {
                backslashCount++;
                continue;
            }

            if (ch == '"')
            {
                builder.Append('\\', backslashCount * 2 + 1);
                builder.Append('"');
                backslashCount = 0;
                continue;
            }

            builder.Append('\\', backslashCount);
            backslashCount = 0;
            builder.Append(ch);
        }

        builder.Append('\\', backslashCount * 2);
        builder.Append('"');
        return builder.ToString();
    }

    private static string EscapeCmdSetValue(string value)
    {
        return value
            .Replace("^", "^^", StringComparison.Ordinal)
            .Replace("%", "%%", StringComparison.Ordinal)
            .Replace("\"", "^\"", StringComparison.Ordinal);
    }

    private static string EscapePowerShellString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private readonly record struct LaunchCommand(string CommandLine);

    private sealed class ForwardingTermPty : TermPTY
    {
        private readonly Action<CommandOutputLine> _onOutput;

        public ForwardingTermPty(Action<CommandOutputLine> onOutput)
        {
            _onOutput = onOutput;
        }

        protected override Span<char> HandleRead(ref ReadState state)
        {
            var parsed = base.HandleRead(ref state);

            if (!parsed.IsEmpty)
            {
                _onOutput(new CommandOutputLine
                {
                    Timestamp = DateTimeOffset.Now,
                    Text = parsed.ToString()
                });
            }

            return parsed;
        }
    }
}
