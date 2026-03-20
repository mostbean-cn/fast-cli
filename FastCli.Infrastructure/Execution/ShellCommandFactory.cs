using System.Diagnostics;
using System.Text;
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
        var commandPayload = BuildCommandPayload(request);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", $"/C {commandPayload}"),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", $"-NoLogo -NoProfile -EncodedCommand {EncodePowerShellCommand(commandPayload)}"),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", $"-NoLogo -NoProfile -EncodedCommand {EncodePowerShellCommand(commandPayload)}"),
            ShellType.Direct => new ProcessStartInfo(request.CommandText, JoinArguments(request.Arguments)),
            _ => throw new InvalidOperationException("不支持的 Shell 类型。")
        };

        startInfo.WorkingDirectory = workingDirectory;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;
        ApplyEnvironmentVariables(startInfo, request);
        return startInfo;
    }

    public static ProcessStartInfo CreateExternalStartInfo(CommandExecutionRequest request)
    {
        var workingDirectory = ResolveWorkingDirectory(request.WorkingDirectory);
        var commandPayload = BuildCommandPayload(request);
        var startInfo = request.ShellType switch
        {
            ShellType.Cmd => new ProcessStartInfo("cmd.exe", $"/K {commandPayload}"),
            ShellType.PowerShell => new ProcessStartInfo("powershell.exe", $"-NoExit -NoLogo -NoProfile -EncodedCommand {EncodePowerShellCommand(commandPayload)}"),
            ShellType.Pwsh => new ProcessStartInfo("pwsh.exe", $"-NoExit -NoLogo -NoProfile -EncodedCommand {EncodePowerShellCommand(commandPayload)}"),
            ShellType.Direct => new ProcessStartInfo("cmd.exe", $"/K {commandPayload}"),
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

    public static string BuildPreview(CommandExecutionRequest request)
    {
        var shellPrefix = request.ShellType switch
        {
            ShellType.Cmd => request.RunMode == CommandRunMode.Embedded ? "cmd.exe /C" : "cmd.exe /K",
            ShellType.PowerShell => request.RunMode == CommandRunMode.Embedded ? "powershell.exe -EncodedCommand" : "powershell.exe -NoExit -EncodedCommand",
            ShellType.Pwsh => request.RunMode == CommandRunMode.Embedded ? "pwsh.exe -EncodedCommand" : "pwsh.exe -NoExit -EncodedCommand",
            ShellType.Direct => request.RunMode == CommandRunMode.Embedded ? "direct" : "cmd.exe /K",
            _ => string.Empty
        };

        var locationPrefix = string.IsNullOrWhiteSpace(request.WorkingDirectory)
            ? string.Empty
            : $"[{request.WorkingDirectory}] ";
        var elevationPrefix = request.RunAsAdministrator ? "[管理员] " : string.Empty;

        return $"{locationPrefix}{elevationPrefix}{shellPrefix} {BuildCommandPayload(request)}".Trim();
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
            return Environment.CurrentDirectory;
        }

        return workingDirectory;
    }
}
