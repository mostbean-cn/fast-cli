using System.Diagnostics;
using System.Text;
using System.Globalization;
using FastCli.Application.Abstractions;
using FastCli.Application.Models;
using FastCli.Domain.Enums;

namespace FastCli.Infrastructure.Execution;

public static class ShellCommandFactory
{
    public static ProcessStartInfo CreateEmbeddedStartInfo(CommandExecutionRequest request, IAppLocalizer localizer)
    {
        if (request.RunAsAdministrator)
        {
            throw new InvalidOperationException(localizer.Get("Service_EmbeddedAdminNotSupported"));
        }

        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", BuildEmbeddedCmdArguments(request)),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", BuildEmbeddedPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", BuildEmbeddedPowerShellArguments(request)),
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

    public static ProcessStartInfo CreateExternalStartInfo(CommandExecutionRequest request, IAppLocalizer localizer)
    {
        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", $"/K {BuildCommandPayload(request)}"),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", BuildExternalPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", BuildExternalPowerShellArguments(request)),
            ShellType.Direct => new ProcessStartInfo("cmd.exe", $"/K {BuildCommandPayload(request)}"),
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

    private static string BuildEmbeddedCmdArguments(CommandExecutionRequest request)
    {
        var payload = BuildCommandPayload(request);
        return string.IsNullOrWhiteSpace(payload)
            ? "/Q"
            : $"/C {payload}";
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

    public static string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return workingDirectory;
    }

    public static string ResolveShellPath(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => "cmd.exe",
            ShellType.PowerShell => "powershell.exe",
            ShellType.Pwsh => "pwsh.exe",
            ShellType.Direct => string.Empty,
            _ => throw new InvalidOperationException($"Unsupported shell type: {shellType}")
        };
    }

    public static string[] BuildConPtyArguments(CommandExecutionRequest request)
    {
        return request.ShellType switch
        {
            ShellType.Cmd => string.IsNullOrWhiteSpace(BuildCommandPayload(request))
                ? []
                : ["/C", BuildCommandPayload(request)],
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
}
