namespace FastCli.Application.Models;

public sealed class CommandSession
{
    private readonly Func<CancellationToken, Task> _stopAsync;

    public CommandSession(Guid executionId, Task<CommandCompletionResult> completion, Func<CancellationToken, Task> stopAsync)
    {
        ExecutionId = executionId;
        Completion = completion;
        _stopAsync = stopAsync;
    }

    public Guid ExecutionId { get; }

    public Task<CommandCompletionResult> Completion { get; }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        return _stopAsync(cancellationToken);
    }
}
