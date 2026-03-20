using System.Diagnostics;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;

namespace FastCli.Infrastructure.Execution;

public sealed class ProcessCommandExecutor : ICommandExecutor
{
    public CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request)
    {
        return ShellCommandFactory.BuildDisplayInfo(request);
    }

    public Task<CommandSession> StartEmbeddedAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        var startInfo = ShellCommandFactory.CreateEmbeddedStartInfo(request);
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
            throw new InvalidOperationException("命令启动失败。");
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
        var startInfo = ShellCommandFactory.CreateExternalStartInfo(request);
        var process = Process.Start(startInfo);

        if (process is null)
        {
            throw new InvalidOperationException("外部终端启动失败。");
        }

        return Task.FromResult(new CommandCompletionResult
        {
            Status = ExecutionStatus.Success,
            Summary = $"已在外部终端启动：{request.Name}"
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

    private static CommandCompletionResult CreateEmbeddedResult(int exitCode, bool canceled)
    {
        if (canceled)
        {
            return new CommandCompletionResult
            {
                Status = ExecutionStatus.Canceled,
                ExitCode = exitCode,
                Summary = "命令已取消。"
            };
        }

        if (exitCode == 0)
        {
            return new CommandCompletionResult
            {
                Status = ExecutionStatus.Success,
                ExitCode = exitCode,
                Summary = "命令执行成功。"
            };
        }

        return new CommandCompletionResult
        {
            Status = ExecutionStatus.Failure,
            ExitCode = exitCode,
            Summary = $"命令执行失败，退出码：{exitCode}"
        };
    }
}
