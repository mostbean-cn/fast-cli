using System.Text;
using EasyWindowsTerminalControl;
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
    private const int MaxTranscriptLength = 200_000;
    private const int TrimmedTranscriptLength = 160_000;

    private readonly object _transcriptSync = new();
    private readonly StringBuilder _rawTranscriptBuilder = new();
    private bool _hasTranscript;

    public TerminalSessionItem(
        TerminalSessionOwnerKind ownerKind,
        ShellType shellType,
        string displayName,
        Guid? commandId = null,
        string? groupName = null)
    {
        SessionId = Guid.NewGuid();
        OwnerKind = ownerKind;
        ShellType = shellType;
        DisplayName = displayName;
        CommandId = commandId;
        GroupName = groupName ?? string.Empty;
        StartedAt = DateTimeOffset.Now;
    }

    public Guid SessionId { get; }

    public TerminalSessionOwnerKind OwnerKind { get; }

    public Guid? CommandId { get; }

    public ShellType ShellType { get; }

    public string DisplayName { get; }

    public string GroupName { get; }

    public DateTimeOffset StartedAt { get; }

    public DateTimeOffset? EndedAt { get; private set; }

    public CommandSession? ExecutorSession { get; private set; }

    public TermPTY? NativeTerminal { get; private set; }

    public bool IsInteractive => ExecutorSession?.IsInteractive == true;

    public bool IsRunning { get; private set; }

    public bool IsClosed { get; private set; }

    public string RawTranscriptText
    {
        get
        {
            lock (_transcriptSync)
            {
                return _rawTranscriptBuilder.ToString();
            }
        }
    }

    public string PlainTranscriptText => AnsiEscapeParser.StripAnsi(RawTranscriptText);

    public bool HasTranscript
    {
        get
        {
            lock (_transcriptSync)
            {
                return _hasTranscript;
            }
        }
    }

    public void Attach(CommandSession session)
    {
        ExecutorSession = session;
        NativeTerminal = session.NativeTerminalHandle as TermPTY;
        IsRunning = true;
        EndedAt = null;
    }

    public bool AppendOutput(string text)
    {
        if (string.IsNullOrEmpty(text) || IsClosed)
        {
            return false;
        }

        lock (_transcriptSync)
        {
            var hadTranscript = _hasTranscript;
            _rawTranscriptBuilder.Append(text);
            TrimTranscriptIfNeeded(_rawTranscriptBuilder);
            _hasTranscript = _rawTranscriptBuilder.Length > 0;
            return !hadTranscript && _hasTranscript;
        }
    }

    public void ClearTranscript()
    {
        lock (_transcriptSync)
        {
            _rawTranscriptBuilder.Clear();
            _hasTranscript = false;
        }
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
        NativeTerminal = null;
    }

    public void MarkClosed()
    {
        IsClosed = true;
        IsRunning = false;
        EndedAt ??= DateTimeOffset.Now;
        ExecutorSession = null;
        NativeTerminal = null;
    }

    private static void TrimTranscriptIfNeeded(StringBuilder builder)
    {
        if (builder.Length <= MaxTranscriptLength)
        {
            return;
        }

        builder.Remove(0, builder.Length - TrimmedTranscriptLength);
    }
}
