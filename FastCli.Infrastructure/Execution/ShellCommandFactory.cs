using System.Diagnostics;
using System.Text;
using System.Globalization;
using FastCli.Application.Models;
using FastCli.Domain.Enums;

namespace FastCli.Infrastructure.Execution;

internal static class ShellCommandFactory
{
    public static ProcessStartInfo CreateEmbeddedStartInfo(CommandExecutionRequest request)
    {
        if (request.RunAsAdministrator)
        {
            throw new InvalidOperationException("V1 暂不支持在应用内以管理员权限执行命令。");
        }

        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", $"/C {BuildCommandPayload(request)}"),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", BuildEmbeddedPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", BuildEmbeddedPowerShellArguments(request)),
            ShellType.Direct => new ProcessStartInfo(request.CommandText, JoinArguments(request.Arguments)),
            _ => throw new InvalidOperationException("不支持的 Shell 类型。")
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

    public static ProcessStartInfo CreateExternalStartInfo(CommandExecutionRequest request)
    {
        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", $"/K {BuildCommandPayload(request)}"),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", BuildExternalPowerShellArguments(request)),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", BuildExternalPowerShellArguments(request)),
            ShellType.Direct => new ProcessStartInfo("cmd.exe", $"/K {BuildCommandPayload(request)}"),
            _ => throw new InvalidOperationException("不支持的 Shell 类型。")
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
            ShellType.Cmd => $"cmd.exe {(request.RunMode == CommandRunMode.Embedded ? "/C" : "/K")} {BuildCommandPayload(request)}",
            ShellType.PowerShell => $"powershell.exe {(request.RunMode == CommandRunMode.Embedded ? "-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand" : "-NoExit -NoLogo -NoProfile -OutputFormat Text -EncodedCommand")} {EncodePowerShellCommand(BuildPowerShellScript(request))}",
            ShellType.Pwsh => $"pwsh.exe {(request.RunMode == CommandRunMode.Embedded ? "-NoLogo -NoProfile -NonInteractive -OutputFormat Text -EncodedCommand" : "-NoExit -NoLogo -NoProfile -OutputFormat Text -EncodedCommand")} {EncodePowerShellCommand(BuildPowerShellScript(request))}",
            ShellType.Direct => request.RunMode == CommandRunMode.Embedded
                ? $"{QuoteIfNeeded(request.CommandText)} {JoinArguments(request.Arguments)}".Trim()
                : $"cmd.exe /K {BuildCommandPayload(request)}",
            _ => string.Empty
        };
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
""" + Environment.NewLine + commandPayload;
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

    private static string ResolveWorkingDirectory(string? workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return workingDirectory;
    }
}
