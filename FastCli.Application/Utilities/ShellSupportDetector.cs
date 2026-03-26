using FastCli.Application.Abstractions;
using FastCli.Domain.Enums;

namespace FastCli.Application.Utilities;

public static class ShellSupportDetector
{
    private static readonly IReadOnlyList<ShellType> CommandShellOrder =
    [
        ShellType.Cmd,
        ShellType.PowerShell,
        ShellType.Pwsh,
        ShellType.Direct
    ];

    private static readonly IReadOnlyList<ShellType> TerminalShellOrder =
    [
        ShellType.Cmd,
        ShellType.PowerShell,
        ShellType.Pwsh
    ];

    public static IReadOnlyList<ShellType> GetAvailableCommandShellTypes()
    {
        return CommandShellOrder
            .Where(IsAvailable)
            .ToArray();
    }

    public static IReadOnlyList<ShellType> GetAvailableTerminalShellTypes()
    {
        return TerminalShellOrder
            .Where(IsAvailable)
            .ToArray();
    }

    public static ShellType GetPreferredCommandShellType()
    {
        return GetAvailableCommandShellTypes().FirstOrDefault(ShellType.Direct);
    }

    public static bool IsAvailable(ShellType shellType)
    {
        return shellType == ShellType.Direct || TryResolveShellPath(shellType, out _);
    }

    public static string ResolveShellPathOrThrow(ShellType shellType, IAppLocalizer localizer)
    {
        if (TryResolveShellPath(shellType, out var shellPath))
        {
            return shellPath;
        }

        throw new InvalidOperationException(
            localizer.Format("Service_ShellExecutableNotFound", GetExecutableName(shellType)));
    }

    public static bool TryResolveShellPath(ShellType shellType, out string shellPath)
    {
        shellPath = shellType switch
        {
            ShellType.Cmd => FindExistingPath(
                "cmd.exe",
                Path.Combine(Environment.SystemDirectory, "cmd.exe")),
            ShellType.PowerShell => FindExistingPath(
                "powershell.exe",
                Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")),
            ShellType.Pwsh => FindExistingPath(
                "pwsh.exe",
                EnumeratePwshCandidates().ToArray()),
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(shellPath);
    }

    public static string GetExecutableName(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => "cmd.exe",
            ShellType.PowerShell => "powershell.exe",
            ShellType.Pwsh => "pwsh.exe",
            ShellType.Direct => string.Empty,
            _ => shellType.ToString()
        };
    }

    private static string FindExistingPath(string executableName, params string[] preferredCandidates)
    {
        foreach (var candidate in preferredCandidates.Where(static path => !string.IsNullOrWhiteSpace(path)))
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        foreach (var pathEntry in EnumeratePathEntries())
        {
            var candidate = Path.Combine(pathEntry, executableName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return string.Empty;
    }

    private static IEnumerable<string> EnumeratePwshCandidates()
    {
        foreach (var programFilesRoot in new[]
                 {
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (string.IsNullOrWhiteSpace(programFilesRoot))
            {
                continue;
            }

            var powerShellRoot = Path.Combine(programFilesRoot, "PowerShell");
            if (!Directory.Exists(powerShellRoot))
            {
                continue;
            }

            IEnumerable<string> versionDirectories;

            try
            {
                versionDirectories = Directory
                    .EnumerateDirectories(powerShellRoot)
                    .OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                continue;
            }

            foreach (var versionDirectory in versionDirectories)
            {
                yield return Path.Combine(versionDirectory, "pwsh.exe");
            }
        }
    }

    private static IEnumerable<string> EnumeratePathEntries()
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
        {
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawEntry in pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalizedEntry = rawEntry.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(normalizedEntry))
            {
                continue;
            }

            if (seen.Add(normalizedEntry))
            {
                yield return normalizedEntry;
            }
        }
    }
}
