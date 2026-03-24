using System.Diagnostics;
using System.IO;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;

namespace FastCli.Infrastructure.Execution;

public sealed class ProcessCommandExecutor : ICommandExecutor
{
    private readonly IAppLocalizer _localizer;

    public ProcessCommandExecutor(IAppLocalizer localizer)
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
        var startInfo = ShellCommandFactory.CreateEmbeddedStartInfo(request, _localizer);
        startInfo.RedirectStandardInput = true;
        startInfo.StandardInputEncoding = System.Text.Encoding.UTF8;

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var completionSource = new TaskCompletionSource<CommandCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;

        if (!process.Start())
        {
            throw new InvalidOperationException(_localizer.Get("Service_CommandStartFailed"));
        }

        var stdoutTask = PumpReaderAsync(process.StandardOutput, isError: false, onOutput);
        var stderrTask = PumpReaderAsync(process.StandardError, isError: true, onOutput);

        var registration = cancellationToken.Register(() =>
        {
            _ = StopProcessAsync(process, () => Interlocked.Exchange(ref canceled, 1));
        });

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
                    var isCanceled = Interlocked.CompareExchange(ref canceled, 0, 0) == 1;
                    completionSource.TrySetResult(CreateEmbeddedResult(process.ExitCode, isCanceled));
                }
                catch (Exception ex)
                {
                    completionSource.TrySetException(ex);
                }
                finally
                {
                    registration.Dispose();
                    process.Dispose();
                }
            },
            CancellationToken.None);

        Func<byte[], CancellationToken, Task> sendInput = async (bytes, ct) =>
        {
            if (process.HasExited)
            {
                return;
            }

            try
            {
                var text = System.Text.Encoding.UTF8.GetString(bytes);
                await process.StandardInput.WriteAsync(text.AsMemory(), ct);
                await process.StandardInput.FlushAsync(ct);
            }
            catch
            {
            }
        };

        return Task.FromResult(new CommandSession(
            request.ExecutionId,
            completionSource.Task,
            _ => StopProcessAsync(process, () => Interlocked.Exchange(ref canceled, 1)))
        {
            SendInputAsync = sendInput
        });
    }

    public Task<CommandCompletionResult> StartExternalAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = ShellCommandFactory.CreateExternalStartInfo(request, _localizer);
        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException(_localizer.Get("Service_ExternalTerminalStartFailed"));
        }

        return Task.FromResult(new CommandCompletionResult
        {
            Status = ExecutionStatus.Success,
            Summary = _localizer.Format("Service_ExternalTerminalStarted", request.Name)
        });
    }

    public Task<CommandSession> StartTerminalAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        return StartEmbeddedAsync(request, onOutput, cancellationToken);
    }

    private static async Task StopProcessAsync(Process process, Action markCanceled)
    {
        try
        {
            markCanceled();

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception)
        {
        }
    }

    private static async Task PumpReaderAsync(
        StreamReader reader,
        bool isError,
        Action<CommandOutputLine> onOutput)
    {
        var buffer = new char[4096];

        try
        {
            while (true)
            {
                var charsRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);

                if (charsRead == 0)
                {
                    break;
                }

                onOutput(new CommandOutputLine
                {
                    Timestamp = DateTimeOffset.Now,
                    Text = new string(buffer, 0, charsRead),
                    IsError = isError
                });
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private CommandCompletionResult CreateEmbeddedResult(int exitCode, bool canceled)
    {
        if (canceled)
        {
            return new CommandCompletionResult
            {
                Status = ExecutionStatus.Canceled,
                ExitCode = exitCode,
                Summary = _localizer.Get("Service_CommandWasCanceled")
            };
        }

        if (exitCode == 0)
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
            Summary = _localizer.Format("Service_CommandFailedWithExitCode", exitCode)
        };
    }
}
