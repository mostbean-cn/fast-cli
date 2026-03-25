using System.Text;
using FastCli.Application.Models;
using FastCli.Application.Utilities;
using FastCli.Domain.Enums;

namespace FastCli.Desktop.ViewModels;

internal enum TerminalSessionOwnerKind
{
    Command = 0,
    AdHoc = 1
}

internal sealed class TerminalSessionItem
{
    private readonly StringBuilder _rawTranscriptBuilder = new();
    private readonly StringBuilder _plainTranscriptBuilder = new();

    public TerminalSessionItem(
        TerminalSessionOwnerKind ownerKind,
        ShellType shellType,
        string displayName,
        Guid? commandId = null)
    {
        SessionId = Guid.NewGuid();
        OwnerKind = ownerKind;
        ShellType = shellType;
        DisplayName = displayName;
        CommandId = commandId;
        StartedAt = DateTimeOffset.Now;
    }

    public Guid SessionId { get; }

    public TerminalSessionOwnerKind OwnerKind { get; }

    public Guid? CommandId { get; }

    public ShellType ShellType { get; }

    public string DisplayName { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; private set; }

    public CommandSession? ExecutorSession { get; private set; }

    public bool IsInteractive => ExecutorSession?.IsInteractive == true;

    public bool IsRunning { get; private set; }

    public bool IsClosed { get; private set; }

    public string RawTranscriptText => _rawTranscriptBuilder.ToString();

    public string PlainTranscriptText => _plainTranscriptBuilder.ToString();

    public void Attach(CommandSession session)
    {
        ExecutorSession = session;
        IsRunning = true;
        EndedAt = null;
    }

    public void AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text) || IsClosed)
        {
            return;
        }

        _rawTranscriptBuilder.Append(text);
        _plainTranscriptBuilder.Append(AnsiEscapeParser.StripAnsi(text));
    }

    public void ClearTranscript()
    {
        _rawTranscriptBuilder.Clear();
        _plainTranscriptBuilder.Clear();
    }

    public void MarkExited()
    {
        if (IsClosed)
        {
            return;
        }

        IsRunning = false;
        EndedAt ??= DateTimeOffset.Now;
        ExecutorSession = null;
    }

    public void MarkClosed()
    {
        IsClosed = true;
        IsRunning = false;
        EndedAt ??= DateTimeOffset.Now;
        ExecutorSession = null;
    }
}
