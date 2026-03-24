using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using FastCli.Desktop.Mvvm;

namespace FastCli.Desktop.Services;

public static class WindowAppearanceService
{
    private const int DwAttributeUseImmersiveDarkMode = 20;
    private static readonly Dictionary<Window, Action<bool>> RegisteredWindows = [];

    public static void Register(Window window)
    {
        if (window.WindowStyle == WindowStyle.None || RegisteredWindows.ContainsKey(window))
        {
            return;
        }

        void applyHandler(bool isDarkTheme) => ApplyTitleBarTheme(window, isDarkTheme);

        RegisteredWindows.Add(window, applyHandler);
        ThemeManager.ThemeChanged += applyHandler;
        window.SourceInitialized += Window_SourceInitialized;
        window.Closed += Window_Closed;

        ApplyTitleBarTheme(window, ThemeManager.IsDarkTheme);
    }

    private static void Window_SourceInitialized(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            ApplyTitleBarTheme(window, ThemeManager.IsDarkTheme);
        }
    }

    private static void Window_Closed(object? sender, EventArgs e)
    {
        if (sender is Window window)
        {
            Unregister(window);
        }
    }

    private static void Unregister(Window window)
    {
        if (RegisteredWindows.Remove(window, out var applyHandler))
        {
            ThemeManager.ThemeChanged -= applyHandler;
        }

        window.SourceInitialized -= Window_SourceInitialized;
        window.Closed -= Window_Closed;
    }

    private static void ApplyTitleBarTheme(Window window, bool isDarkTheme)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;

        if (handle == IntPtr.Zero)
        {
            return;
        }

        var darkModeValue = isDarkTheme ? 1 : 0;

        try
        {
            _ = DwmSetWindowAttribute(
                handle,
                DwAttributeUseImmersiveDarkMode,
                ref darkModeValue,
                Marshal.SizeOf<int>());
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int dwAttribute,
        ref int pvAttribute,
        int cbAttribute);
}
