(function () {
  const root = document.getElementById('terminal-root');
  const fitAddon = new FitAddon.FitAddon();
  const terminal = new Terminal({
    allowTransparency: false,
    convertEol: false,
    cursorBlink: true,
    cursorInactiveStyle: 'outline',
    drawBoldTextInBrightColors: true,
    fontFamily: '"Cascadia Mono", Consolas, monospace',
    fontSize: 13,
    lineHeight: 1.12,
    scrollback: 5000,
    theme: {
      background: '#0d1117',
      foreground: '#e6edf3',
      cursor: '#ffffff',
      cursorAccent: '#000000',
      selectionBackground: '#264f78'
    }
  });

  const postMessage = payload => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(payload);
    }
  };

  const fit = () => {
    if (!root || root.clientWidth === 0 || root.clientHeight === 0) {
      return;
    }

    fitAddon.fit();
  };

  const fitDeferred = () => {
    window.requestAnimationFrame(() => {
      fit();
      window.requestAnimationFrame(() => {
        fit();
      });
    });
  };

  terminal.loadAddon(fitAddon);
  terminal.open(root);
  fitDeferred();
  terminal.focus();
  terminal.writeln('FastCli terminal ready. Run an embedded command to start an interactive shell.');

  terminal.onData(data => {
    postMessage({ type: 'input', data });
  });

  terminal.onResize(size => {
    postMessage({ type: 'resize', cols: size.cols, rows: size.rows });
  });

  new ResizeObserver(() => {
    fitDeferred();
  }).observe(root);

  window.fastCliTerminal = {
    focus() {
      terminal.focus();
    },
    write(data) {
      if (typeof data === 'string' && data.length > 0) {
        terminal.write(data);
      }
    },
    syncViewport() {
      const refresh = () => {
        fit();
        terminal.refresh(0, Math.max(terminal.rows - 1, 0));
      };

      fitDeferred();
      window.setTimeout(refresh, 24);
      window.setTimeout(refresh, 72);
      window.setTimeout(refresh, 144);
    },
    replace(data) {
      terminal.reset();
      if (typeof data === 'string' && data.length > 0) {
        terminal.write(data);
      }
      window.fastCliTerminal.syncViewport();
    },
    setTheme(theme) {
      if (!theme || typeof theme !== 'object') {
        return;
      }

      terminal.options.theme = {
        background: theme.background,
        foreground: theme.foreground,
        cursor: theme.cursor,
        cursorAccent: theme.cursorAccent,
        selectionBackground: theme.selectionBackground,
        black: theme.black,
        red: theme.red,
        green: theme.green,
        yellow: theme.yellow,
        blue: theme.blue,
        magenta: theme.magenta,
        cyan: theme.cyan,
        white: theme.white,
        brightBlack: theme.brightBlack,
        brightRed: theme.brightRed,
        brightGreen: theme.brightGreen,
        brightYellow: theme.brightYellow,
        brightBlue: theme.brightBlue,
        brightMagenta: theme.brightMagenta,
        brightCyan: theme.brightCyan,
        brightWhite: theme.brightWhite
      };

      document.body.style.background = theme.background;
      document.body.style.color = theme.foreground;
      window.fastCliTerminal.syncViewport();
    }
  };

  postMessage({ type: 'ready' });
})();
