using System.Text;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Application.Utilities;
using FastCli.Domain.Enums;
using Porta.Pty;

namespace FastCli.Infrastructure.Execution;

public sealed class ConPtyCommandExecutor : ICommandExecutor
{
    private readonly IAppLocalizer _localizer;

    public ConPtyCommandExecutor(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<CommandSession> StartEmbeddedAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        if (request.RunAsAdministrator)
        {
            throw new InvalidOperationException(_localizer.Get("Service_EmbeddedAdminNotSupported"));
        }

        var temporaryCmdScriptPath = ShellCommandFactory.CreateTemporaryCmdScriptIfNeeded(request);
        IPtyConnection connection;

        try
        {
            var options = BuildPtyOptions(request, temporaryCmdScriptPath);
            connection = await PtyProvider.SpawnAsync(options, cancellationToken);
        }
        catch
        {
            ShellCommandFactory.TryDeleteTemporaryScript(temporaryCmdScriptPath);
            throw;
        }

        var completionSource = new TaskCompletionSource<CommandCompletionResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;
        var connectionClosed = 0;
        var temporaryScriptDeleted = 0;

        void CleanupTemporaryScript()
        {
            if (Interlocked.Exchange(ref temporaryScriptDeleted, 1) == 0)
            {
                ShellCommandFactory.TryDeleteTemporaryScript(temporaryCmdScriptPath);
            }
        }

        connection.ProcessExited += (_, e) =>
        {
            var isCanceled = Interlocked.CompareExchange(ref canceled, 0, 0) == 1;

            if (isCanceled)
            {
                completionSource.TrySetResult(new CommandCompletionResult
                {
                    Status = ExecutionStatus.Canceled,
                    ExitCode = e.ExitCode,
                    Summary = _localizer.Get("Service_CommandWasCanceled")
                });
            }
            else if (e.ExitCode == 0)
            {
                completionSource.TrySetResult(new CommandCompletionResult
                {
                    Status = ExecutionStatus.Success,
                    ExitCode = e.ExitCode,
                    Summary = _localizer.Get("Service_CommandSucceeded")
                });
            }
            else
            {
                completionSource.TrySetResult(new CommandCompletionResult
                {
                    Status = ExecutionStatus.Failure,
                    ExitCode = e.ExitCode,
                    Summary = _localizer.Format("Service_CommandFailedWithExitCode", e.ExitCode)
                });
            }

            CleanupTemporaryScript();
        };

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await ReadOutputLoop(connection, onOutput, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                }
            },
            CancellationToken.None);

        Func<byte[], CancellationToken, Task> sendInput = async (bytes, ct) =>
        {
            if (Interlocked.CompareExchange(ref connectionClosed, 0, 0) == 1)
            {
                return;
            }

            try
            {
                await connection.WriterStream.WriteAsync(bytes, 0, bytes.Length, ct);
                await connection.WriterStream.FlushAsync(ct);
            }
            catch
            {
            }
        };

        Func<int, int, CancellationToken, Task> resizeAsync = (cols, rows, _) =>
        {
            if (Interlocked.CompareExchange(ref connectionClosed, 0, 0) == 1)
            {
                return Task.CompletedTask;
            }

            try
            {
                connection.Resize(cols, rows);
            }
            catch
            {
            }

            return Task.CompletedTask;
        };

        Func<CancellationToken, Task> stopAsync = ct =>
        {
            Interlocked.Exchange(ref canceled, 1);
            Interlocked.Exchange(ref connectionClosed, 1);

            try
            {
                connection.Kill();
            }
            catch
            {
            }

            completionSource.TrySetResult(new CommandCompletionResult
            {
                Status = ExecutionStatus.Canceled,
                Summary = _localizer.Get("Service_CommandWasCanceled")
            });

            CleanupTemporaryScript();

            return Task.CompletedTask;
        };

        return new CommandSession(request.ExecutionId, completionSource.Task, stopAsync)
        {
            SendInputAsync = sendInput,
            ResizeAsync = resizeAsync
        };
    }

    public async Task<CommandCompletionResult> StartExternalAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var fallback = new ProcessCommandExecutor(_localizer);
        return await fallback.StartExternalAsync(request, cancellationToken);
    }

    public Task<CommandSession> StartTerminalAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        return StartEmbeddedAsync(request, onOutput, cancellationToken);
    }

    public CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request)
    {
        return ShellCommandFactory.BuildDisplayInfo(request);
    }

    private async Task ReadOutputLoop(
        IPtyConnection connection,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var bytesRead = await connection.ReaderStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                if (bytesRead == 0)
                {
                    break;
                }

                var text = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                onOutput(new CommandOutputLine
                {
                    Timestamp = DateTimeOffset.Now,
                    Text = text
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                break;
            }
        }
    }

    private PtyOptions BuildPtyOptions(CommandExecutionRequest request, string? temporaryCmdScriptPath)
    {
        var shellPath = request.ShellType == ShellType.Direct
            ? string.Empty
            : ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, _localizer);
        var workingDir = ShellCommandFactory.ResolveWorkingDirectory(request.WorkingDirectory);

        if (request.ShellType == ShellType.Direct)
        {
            shellPath = request.CommandText;
        }

        var environment = new Dictionary<string, string>
        {
            ["TERM"] = "xterm-256color",
            ["COLORTERM"] = "truecolor"
        };

        foreach (var item in request.EnvironmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                environment[item.Key] = item.Value ?? string.Empty;
            }
        }

        return new PtyOptions
        {
            Name = request.Name,
            Cols = 120,
            Rows = 30,
            Cwd = workingDir,
            App = shellPath,
            CommandLine = ShellCommandFactory.BuildConPtyArguments(request, temporaryCmdScriptPath),
            Environment = environment
        };
    }
}
