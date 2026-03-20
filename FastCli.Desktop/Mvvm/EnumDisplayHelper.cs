using FastCli.Domain.Enums;

namespace FastCli.Desktop.Mvvm;

public static class EnumDisplayHelper
{
    public static string ToDisplayText(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => "命令提示符",
            ShellType.PowerShell => "Windows PowerShell",
            ShellType.Pwsh => "PowerShell 7",
            ShellType.Direct => "直接启动",
            _ => shellType.ToString()
        };
    }
}
