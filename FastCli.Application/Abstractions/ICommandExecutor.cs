using FastCli.Application.Models;

namespace FastCli.Application.Abstractions;

public interface ICommandExecutor
{
    Task<CommandSession> StartEmbeddedAsync(
        CommandExecutionRequest request,
        Action<CommandOutputLine> onOutput,
        CancellationToken cancellationToken = default);

    Task<CommandCompletionResult> StartExternalAsync(
        CommandExecutionRequest request,
        CancellationToken cancellationToken = default);

    CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request);
}
