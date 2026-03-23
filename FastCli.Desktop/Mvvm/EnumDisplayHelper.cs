using FastCli.Domain.Enums;
using FastCli.Desktop.Localization;

namespace FastCli.Desktop.Mvvm;

public static class EnumDisplayHelper
{
    public static string ToDisplayText(ShellType shellType)
    {
        return shellType switch
        {
            ShellType.Cmd => LocalizationManager.Instance.Get("Shell_CmdDisplay"),
            ShellType.PowerShell => LocalizationManager.Instance.Get("Shell_PowerShellDisplay"),
            ShellType.Pwsh => LocalizationManager.Instance.Get("Shell_PwshDisplay"),
            ShellType.Direct => LocalizationManager.Instance.Get("Shell_DirectDisplay"),
            _ => shellType.ToString()
        };
    }
}
