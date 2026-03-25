using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Reflection;
using System.Runtime.InteropServices;
using EasyWindowsTerminalControl;
using Microsoft.Terminal.Wpf;

namespace FastCli.Desktop.Terminal;

public sealed class WindowsTerminalControlHost
{
    private const string DefaultFontFamily = "Cascadia Code";
    private const short DefaultFontSize = 13;
    private static readonly FieldInfo? TerminalContainerField = typeof(TerminalControl)
        .GetField("termContainer", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly PropertyInfo? TerminalHandleProperty = typeof(TerminalControl).Assembly
        .GetType("Microsoft.Terminal.Wpf.TerminalContainer")
        ?.GetProperty("Hwnd", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private readonly Grid _host;
    private TerminalControl _terminalControl;
    private TerminalTheme _theme = new();
    private TermPTY? _displayedTerm;
    private TermPTY? _readySubscribedTerm;
    private TermPTY? _connectedTerm;
    private string _displayedTranscript = string.Empty;
    private bool _displayedReadOnly;
    private bool _displayFocusRequested;
    private bool _isLoaded;

    public event Action<Exception>? Faulted;

    public WindowsTerminalControlHost(Grid host)
    {
        _host = host;
        _terminalControl = CreateTerminalControl();
        _host.Children.Insert(0, _terminalControl);
    }

    public Task InitializeAsync(TerminalTheme theme)
    {
        _theme = theme;
        ApplyThemeIfReady();
        return Task.CompletedTask;
    }

    public Task SetThemeAsync(TerminalTheme theme)
    {
        _theme = theme;
        ApplyThemeIfReady();
        return Task.CompletedTask;
    }

    public async Task DisplayAsync(
        TermPTY? terminal,
        string transcript,
        bool allowInput,
        bool requestFocus)
    {
        _displayedTerm = terminal;
        _displayedTranscript = transcript ?? string.Empty;
        _displayedReadOnly = !allowInput;
        _displayFocusRequested = requestFocus;
        DetachReadySubscription();

        if (!_host.Dispatcher.CheckAccess())
        {
            await _host.Dispatcher.InvokeAsync(AttachCurrentSession);
            return;
        }

        AttachCurrentSession();
    }

    public async Task SyncAsync(bool requestFocus = false)
    {
        if (!_host.Dispatcher.CheckAccess())
        {
            await _host.Dispatcher.InvokeAsync(() => SyncCurrentSession(requestFocus));
            return;
        }

        SyncCurrentSession(requestFocus);
    }

    private TerminalControl CreateTerminalControl()
    {
        var control = new TerminalControl
        {
            AutoResize = true,
            Focusable = true,
            IsTabStop = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Margin = new Thickness(0),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true
        };

        InputMethod.SetIsInputMethodEnabled(control, true);
        control.Loaded += TerminalControl_Loaded;
        control.Unloaded += TerminalControl_Unloaded;
        return control;
    }

    private void TerminalControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _isLoaded = true;
            ApplyThemeIfReady();
            AttachCurrentSession();
        }
        catch (Exception ex)
        {
            ReportFault(ex);
        }
    }

    private void TerminalControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        DetachReadySubscription();
    }

    private void ApplyThemeIfReady()
    {
        if (!_isLoaded)
        {
            return;
        }

        _terminalControl.SetTheme(
            CreateNativeTheme(_theme),
            DefaultFontFamily,
            DefaultFontSize,
            ToMediaColor(_theme.Background));
    }

    private void AttachCurrentSession()
    {
        if (!_isLoaded)
        {
            return;
        }

        ApplyThemeIfReady();

        if (_displayedTerm is null)
        {
            _terminalControl.Connection = null;
            _connectedTerm = null;
            return;
        }

        if (!_displayedTerm.TermProcIsStarted)
        {
            _terminalControl.Connection = null;
            _connectedTerm = null;
            _displayedTerm.TermReady += DisplayedTerm_TermReady;
            _readySubscribedTerm = _displayedTerm;
            return;
        }

        ApplyConnectionState();
    }

    private void DisplayedTerm_TermReady(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _displayedTerm))
        {
            return;
        }

        _ = _host.Dispatcher.InvokeAsync(() =>
        {
            try
            {
                DetachReadySubscription();
                ApplyConnectionState();
            }
            catch (Exception ex)
            {
                ReportFault(ex);
            }
        });
    }

    private void ApplyConnectionState()
    {
        if (_displayedTerm is null || !_isLoaded)
        {
            return;
        }

        if (!ReferenceEquals(_connectedTerm, _displayedTerm))
        {
            _terminalControl.Connection = _displayedTerm;
            _connectedTerm = _displayedTerm;
        }

        _displayedTerm.SetReadOnly(_displayedReadOnly, updateCursor: false);
        _displayedTerm.Win32DirectInputMode(true);
        ReplayTranscript();
        SyncCurrentSession(_displayFocusRequested);
    }

    private void ReplayTranscript()
    {
        if (_displayedTerm is null || !_displayedTerm.TermProcIsStarted)
        {
            return;
        }

        _displayedTerm.ClearUITerminal(fullReset: true);

        if (!string.IsNullOrEmpty(_displayedTranscript))
        {
            _displayedTerm.WriteToUITerminal(_displayedTranscript.AsSpan());
        }
    }

    private void SyncCurrentSession(bool requestFocus)
    {
        if (_displayedTerm is null || !_displayedTerm.TermProcIsStarted || !_isLoaded)
        {
            return;
        }

        var columns = Math.Max((int)_terminalControl.Columns, 1);
        var rows = Math.Max((int)_terminalControl.Rows, 1);

        _displayedTerm.Resize(columns, rows);

        if (requestFocus)
        {
            FocusTerminalControl();
        }
    }

    private static Microsoft.Terminal.Wpf.TerminalTheme CreateNativeTheme(TerminalTheme theme)
    {
        return new Microsoft.Terminal.Wpf.TerminalTheme
        {
            DefaultBackground = ToColorRef(theme.Background),
            DefaultForeground = ToColorRef(theme.Foreground),
            DefaultSelectionBackground = ToColorRef(theme.SelectionBackground),
            CursorStyle = CursorStyle.SteadyBar,
            ColorTable =
            [
                ToColorRef(theme.Black),
                ToColorRef(theme.Red),
                ToColorRef(theme.Green),
                ToColorRef(theme.Yellow),
                ToColorRef(theme.Blue),
                ToColorRef(theme.Magenta),
                ToColorRef(theme.Cyan),
                ToColorRef(theme.White),
                ToColorRef(theme.BrightBlack),
                ToColorRef(theme.BrightRed),
                ToColorRef(theme.BrightGreen),
                ToColorRef(theme.BrightYellow),
                ToColorRef(theme.BrightBlue),
                ToColorRef(theme.BrightMagenta),
                ToColorRef(theme.BrightCyan),
                ToColorRef(theme.BrightWhite)
            ]
        };
    }

    private static uint ToColorRef(string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is not Color color)
        {
            return 0;
        }

        return (uint)(color.R | (color.G << 8) | (color.B << 16));
    }

    private void DetachReadySubscription()
    {
        if (_readySubscribedTerm is null)
        {
            return;
        }

        _readySubscribedTerm.TermReady -= DisplayedTerm_TermReady;
        _readySubscribedTerm = null;
    }

    private void FocusTerminalControl()
    {
        _terminalControl.Focus();
        Keyboard.Focus(_terminalControl);

        var terminalHandle = GetTerminalHandle();

        if (terminalHandle != IntPtr.Zero)
        {
            SetFocus(terminalHandle);
        }
    }

    private IntPtr GetTerminalHandle()
    {
        var terminalContainer = TerminalContainerField?.GetValue(_terminalControl);

        if (terminalContainer is null)
        {
            return IntPtr.Zero;
        }

        if (TerminalHandleProperty?.GetValue(terminalContainer) is IntPtr handle)
        {
            return handle;
        }

        return IntPtr.Zero;
    }

    private static Color ToMediaColor(string hex)
    {
        if (ColorConverter.ConvertFromString(hex) is Color color)
        {
            return color;
        }

        return Colors.Black;
    }

    private void ReportFault(Exception ex)
    {
        Faulted?.Invoke(ex);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetFocus(IntPtr hWnd);
}
