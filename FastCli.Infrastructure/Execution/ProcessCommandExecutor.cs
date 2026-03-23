using System.Diagnostics;
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
        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        var completionSource = new TaskCompletionSource<CommandCompletionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var canceled = 0;

        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrEmpty(eventArgs.Data))
            {
                onOutput(new CommandOutputLine
                {
                    Timestamp = DateTimeOffset.Now,
                    Text = eventArgs.Data,
                    IsError = false
                });
            }
        };

        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (!string.IsNullOrEmpty(eventArgs.Data))
            {
                onOutput(new CommandOutputLine
                {
                    Timestamp = DateTimeOffset.Now,
                    Text = eventArgs.Data,
                    IsError = true
                });
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(_localizer.Get("Service_CommandStartFailed"));
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

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
                    process.WaitForExit();
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

        return Task.FromResult(new CommandSession(
            request.ExecutionId,
            completionSource.Task,
            _ => StopProcessAsync(process, () => Interlocked.Exchange(ref canceled, 1))));
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
