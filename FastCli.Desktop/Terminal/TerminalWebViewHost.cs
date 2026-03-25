using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace FastCli.Desktop.Terminal;

public sealed class TerminalWebViewHost
{
    private const string HostName = "terminal.fastcli.local";

    private readonly WebView2 _webView;
    private readonly string _assetDirectory;
    private readonly Queue<string> _pendingScripts = new();
    private Func<string, Task>? _inputHandler;
    private Func<int, int, Task>? _resizeHandler;
    private bool _isReady;

    public TerminalWebViewHost(WebView2 webView, string assetDirectory)
    {
        _webView = webView;
        _assetDirectory = assetDirectory;
    }

    public async Task InitializeAsync(
        TerminalTheme theme,
        Func<string, Task> inputHandler,
        Func<int, int, Task> resizeHandler)
    {
        _inputHandler = inputHandler;
        _resizeHandler = resizeHandler;

        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FastCli",
            "WebView2");

        Directory.CreateDirectory(userDataFolder);

        var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await _webView.EnsureCoreWebView2Async(environment);

        _webView.DefaultBackgroundColor = Color.Transparent;
        _webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        _webView.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
        _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
        _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
        _webView.CoreWebView2.NavigationStarting += CoreWebView2_NavigationStarting;
        _webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            HostName,
            _assetDirectory,
            CoreWebView2HostResourceAccessKind.Allow);

        await SetThemeAsync(theme);

        var sourceUri = new Uri($"https://{HostName}/index.html");

        if (_webView.Source != sourceUri)
        {
            _webView.Source = sourceUri;
        }
    }

    public Task WriteAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Task.CompletedTask;
        }

        return ExecuteScriptAsync($"window.fastCliTerminal.write({JsonSerializer.Serialize(text)});");
    }

    public Task ReplaceAsync(string text)
    {
        return ExecuteScriptAsync($"window.fastCliTerminal.replace({JsonSerializer.Serialize(text ?? string.Empty)});");
    }

    public Task HardRefreshAsync()
    {
        return ExecuteScriptAsync("window.fastCliTerminal.hardRefresh();");
    }

    public Task FocusAsync()
    {
        return ExecuteScriptAsync("window.fastCliTerminal.focus();");
    }

    public Task SyncViewportAsync(
        string reason = "manual",
        bool requestFocus = false,
        bool preserveBottom = true)
    {
        var payload = JsonSerializer.Serialize(new
        {
            reason,
            requestFocus,
            preserveBottom
        });

        return ExecuteScriptAsync($"window.fastCliTerminal.syncViewport({payload});");
    }

    public Task SetThemeAsync(TerminalTheme theme)
    {
        return ExecuteScriptAsync($"window.fastCliTerminal.setTheme({JsonSerializer.Serialize(theme)});");
    }

    private Task ExecuteScriptAsync(string script)
    {
        if (_webView.CoreWebView2 is null || !_isReady)
        {
            _pendingScripts.Enqueue(script);
            return Task.CompletedTask;
        }

        return _webView.CoreWebView2.ExecuteScriptAsync(script);
    }

    private void CoreWebView2_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith($"https://{HostName}/", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
        }
    }

    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        if (!TerminalWebMessage.TryParse(e.WebMessageAsJson, out var message))
        {
            return;
        }

        switch (message.Type)
        {
            case "ready":
                _isReady = true;

                while (_pendingScripts.Count > 0)
                {
                    await _webView.CoreWebView2.ExecuteScriptAsync(_pendingScripts.Dequeue());
                }

                break;
            case "input":
                if (_inputHandler is not null && message.Data is not null)
                {
                    await _inputHandler(message.Data);
                }

                break;
            case "resize":
                if (_resizeHandler is not null && message.Cols.HasValue && message.Rows.HasValue)
                {
                    await _resizeHandler(message.Cols.Value, message.Rows.Value);
                }

                break;
        }
    }
}
