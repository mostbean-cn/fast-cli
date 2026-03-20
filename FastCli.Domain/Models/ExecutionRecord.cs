using FastCli.Domain.Enums;

namespace FastCli.Domain.Models;

public sealed class ExecutionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CommandProfileId { get; set; }

    public string TriggerSource { get; set; } = "manual";

    public CommandRunMode RunMode { get; set; }

    public ExecutionStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? EndedAt { get; set; }

    public int? ExitCode { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string OutputText { get; set; } = string.Empty;
}
