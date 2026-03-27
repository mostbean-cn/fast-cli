using System.Diagnostics;
using System.Text;
using System.Globalization;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Application.Utilities;
using FastCli.Domain.Enums;

namespace FastCli.Infrastructure.Execution;

internal static class ShellCommandFactory
{
    public static ProcessStartInfo CreateEmbeddedStartInfo(
        CommandExecutionRequest request,
        IAppLocalizer localizer,
        string? temporaryCmdScriptPath = null)
    {
        if (request.RunAsAdministrator)
        {
            throw new InvalidOperationException(localizer.Get("Service_EmbeddedAdminNotSupported"));
        }

        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo(
                ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer),
                BuildEmbeddedCmdArguments(request, temporaryCmdScriptPath)),
            ShellType.PowerShell => new ProcessStartInfo(ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer), BuildEmbeddedPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo(ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer), BuildEmbeddedPowerShellArguments(request)),
            ShellType.Direct => new ProcessStartInfo(request.CommandText, JoinArguments(request.Arguments)),
            _ => throw new InvalidOperationException(localizer.Get("Service_UnsupportedShellType"))
        };

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = GetEmbeddedOutputEncoding(request.ShellType);
        startInfo.StandardErrorEncoding = GetEmbeddedOutputEncoding(request.ShellType);
        ApplyEnvironmentVariables(startInfo, request);
        return startInfo;
    }

    public static ProcessStartInfo CreateExternalStartInfo(
        CommandExecutionRequest request,
        IAppLocalizer localizer,
        string? temporaryCmdScriptPath = null)
    {
        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo(
                ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer),
                BuildExternalCmdArguments(request, temporaryCmdScriptPath)),
            ShellType.PowerShell => new ProcessStartInfo(ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer), BuildExternalPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo(ShellSupportDetector.ResolveShellPathOrThrow(request.ShellType, localizer), BuildExternalPowerShellArguments(request)),
            ShellType.Direct => new ProcessStartInfo(ShellSupportDetector.ResolveShellPathOrThrow(ShellType.Cmd, localizer), $"/K {BuildCommandPayload(request)}"),
            _ => throw new InvalidOperationException(localizer.Get("Service_UnsupportedShellType"))
        };

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = true;
        startInfo.CreateNoWindow = false;

        if (request.RunAsAdministrator)
        {
            startInfo.Verb = "runas";
        }

        return startInfo;
    }

    public static CommandDisplayInfo BuildDisplayInfo(CommandExecutionRequest request)
    {
        return new CommandDisplayInfo
        {
            UserReadablePreview = BuildUserReadablePreview(request),
            ActualExecutionCommand = BuildActualExecutionCommand(request)
        };
    }

    private static string BuildCommandPayload(CommandExecutionRequest request)
    {
        if (request.ShellType == ShellType.Direct)
        {
            return $"{QuoteIfNeeded(request.CommandText)} {JoinArguments(request.Arguments)}".Trim();
        }

        var builder = new StringBuilder(request.CommandText.Trim());

        if (request.Arguments.Count > 0)
        {
            builder.Append(' ');
            builder.Append(JoinArguments(request.Arguments));
        }

        return builder.ToString().Trim();
    }

    private static string JoinArguments(IReadOnlyList<string> arguments)
    {
        return string.Join(" ", arguments.Where(static item => !string.IsNullOrWhiteSpace(item)).Select(QuoteIfNeeded));
    }

    private static string QuoteIfNeeded(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "\"\"";
        }

        return value.Contains(' ') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
    }

    private static string EncodePowerShellCommand(string commandText)
    {
        return Convert.ToBase64String(Encoding.Unicode.GetBytes(commandText));
    }

    private static string BuildUserReadablePreview(CommandExecutionRequest request)
    {
        return BuildCommandPayload(request);
    }

    private static string BuildActualExecutionCommand(CommandExecutionRequest request)
    {
        return request.ShellType switch
        {
            ShellType.Cmd => $"cmd.exe {(request.RunMode == CommandRunMode.Embedded ? BuildEmbeddedCmdArguments(request) : $"/K {BuildCommandPayload(request)}")}".Trim(),
            ShellType.PowerShell => $"powershell.exe {(request.RunMode == CommandRunMode.Embedded ? BuildEmbeddedPowerShellArguments(request) : BuildExternalPowerShellArguments(request))}",
            ShellType.Pwsh => $"pwsh.exe {(request.RunMode == CommandRunMode.Embedded ? BuildEmbeddedPowerShellArguments(request) : BuildExternalPowerShellArguments(request))}",
            ShellType.Direct => request.RunMode == CommandRunMode.Embedded
                ? $"{QuoteIfNeeded(request.CommandText)} {JoinArguments(request.Arguments)}".Trim()
                : $"cmd.exe /K {BuildCommandPayload(request)}",
            _ => string.Empty
        };
    }

    internal static string? CreateTemporaryCmdScriptIfNeeded(CommandExecutionRequest request)
    {
        if (request.ShellType != ShellType.Cmd || !LooksLikeMultiLineScript(request.CommandText))
        {
            return null;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "FastCli", "scripts");
        Directory.CreateDirectory(tempRoot);

        var scriptPath = Path.Combine(tempRoot, $"fastcli-{request.ExecutionId:N}.cmd");
        var scriptContent = request.CommandText.Replace("\r\n", "\n").Replace("\n", "\r\n");
        File.WriteAllText(scriptPath, scriptContent, new UTF8Encoding(false));
        return scriptPath;
    }

    internal static void TryDeleteTemporaryScript(string? scriptPath)
    {
        if (string.IsNullOrWhiteSpace(scriptPath) || !File.Exists(scriptPath))
        {
            return;
        }

        try
        {
            File.Delete(scriptPath);
        }
        catch
        {
        }
    }

    private static string BuildEmbeddedCmdArguments(CommandExecutionRequest request, string? temporaryCmdScriptPath = null)
    {
        var payload = BuildCmdPayload(request, temporaryCmdScriptPath);
        return string.IsNullOrWhiteSpace(payload)
            ? "/D /Q"
            : $"/D /C {payload}";
    }

    private static string BuildExternalCmdArguments(CommandExecutionRequest request, string? temporaryCmdScriptPath = null)
    {
        var payload = BuildCmdPayload(request, temporaryCmdScriptPath);
        return string.IsNullOrWhiteSpace(payload)
            ? "/D /K"
            : $"/D /K {payload}";
    }

    private static string BuildEmbeddedPowerShellArguments(CommandExecutionRequest request)
    {
        var script = BuildPowerShellScript(request);
        return $"-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand {EncodePowerShellCommand(script)}";
    }

    private static string BuildExternalPowerShellArguments(CommandExecutionRequest request)
    {
        var script = BuildPowerShellScript(request);
        return $"-NoExit -NoLogo -NoProfile -OutputFormat Text -EncodedCommand {EncodePowerShellCommand(script)}";
    }

    private static string BuildPowerShellScript(CommandExecutionRequest request)
    {
        var commandPayload = BuildCommandPayload(request);
        return """
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [Console]::OutputEncoding
if ($PSVersionTable.PSVersion.Major -ge 7) { $PSStyle.OutputRendering = 'PlainText' }
""" + (string.IsNullOrWhiteSpace(commandPayload) ? string.Empty : Environment.NewLine + commandPayload);
    }

    private static Encoding GetEmbeddedOutputEncoding(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage),
            ShellType.PowerShell => Encoding.UTF8,
            ShellType.Pwsh => Encoding.UTF8,
            _ => Encoding.UTF8
        };
    }

    private static void ApplyEnvironmentVariables(ProcessStartInfo startInfo, CommandExecutionRequest request)
    {
        foreach (var item in request.EnvironmentVariables)
        {
            if (!string.IsNullOrWhiteSpace(item.Key))
            {
                startInfo.Environment[item.Key] = item.Value ?? string.Empty;
            }
        }
    }

    internal static string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return workingDirectory;
    }

    internal static string[] BuildConPtyArguments(CommandExecutionRequest request, string? temporaryCmdScriptPath = null)
    {
        return request.ShellType switch
        { 
            ShellType.Cmd => string.IsNullOrWhiteSpace(BuildCmdPayload(request, temporaryCmdScriptPath))
                ? ["/D"]
                : ["/D", "/C", BuildCmdPayload(request, temporaryCmdScriptPath)],
            ShellType.PowerShell => string.IsNullOrWhiteSpace(BuildCommandPayload(request))
                ? ["-NoLogo", "-NoProfile"]
                : ["-NoLogo", "-NoProfile", "-Command", BuildCommandPayload(request)],
            ShellType.Pwsh => string.IsNullOrWhiteSpace(BuildCommandPayload(request))
                ? ["-NoLogo", "-NoProfile"]
                : ["-NoLogo", "-NoProfile", "-Command", BuildCommandPayload(request)],
            ShellType.Direct => [.. request.Arguments],
            _ => []
        };
    }

    private static string BuildCmdPayload(CommandExecutionRequest request, string? temporaryCmdScriptPath)
    {
        if (!string.IsNullOrWhiteSpace(temporaryCmdScriptPath))
        {
            return BuildCmdScriptInvocation(temporaryCmdScriptPath, request.Arguments);
        }

        return BuildCommandPayload(request);
    }

    private static string BuildCmdScriptInvocation(string scriptPath, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();
        builder.Append("call ");
        builder.Append(QuoteCmdCommandText(scriptPath));

        if (arguments.Count > 0)
        {
            builder.Append(' ');
            builder.Append(JoinArguments(arguments));
        }

        return builder.ToString();
    }

    private static string QuoteCmdCommandText(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static bool LooksLikeMultiLineScript(string commandText)
    {
        return !string.IsNullOrWhiteSpace(commandText)
               && (commandText.Contains('\n') || commandText.Contains('\r'));
    }

}
