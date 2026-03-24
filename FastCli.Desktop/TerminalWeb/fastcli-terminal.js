(function () {
  const root = document.getElementById('terminal-root');
  const fitAddon = new FitAddon.FitAddon();
  const defaultViewportSyncRequest = {
    reason: 'manual',
    requestFocus: false,
    preserveBottom: true
  };

  let viewportSyncTimer = 0;
  let isComposing = false;
  let hasTerminalFocus = false;
  let pendingViewportSync = null;

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

  const applyThemeVariables = theme => {
    const styles = document.documentElement.style;
    styles.setProperty('--terminal-bg', theme.background);
    styles.setProperty('--terminal-fg', theme.foreground);
    styles.setProperty('--terminal-selection', theme.selectionBackground);
    styles.setProperty(
      '--terminal-scrollbar-thumb',
      theme.background.toLowerCase() === '#ffffff' ? 'rgba(107, 114, 128, 0.35)' : 'rgba(148, 163, 184, 0.45)');
  };

  const postMessage = payload => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(payload);
    }
  };

  const normalizeViewportSyncRequest = rawRequest => {
    if (!rawRequest || typeof rawRequest !== 'object') {
      return { ...defaultViewportSyncRequest };
    }

    return {
      reason: typeof rawRequest.reason === 'string' && rawRequest.reason.length > 0
        ? rawRequest.reason
        : defaultViewportSyncRequest.reason,
      requestFocus: rawRequest.requestFocus === true,
      preserveBottom: rawRequest.preserveBottom !== false
    };
  };

  const mergeViewportSyncRequest = request => {
    if (!pendingViewportSync) {
      pendingViewportSync = request;
      return;
    }

    pendingViewportSync = {
      reason: request.reason,
      requestFocus: pendingViewportSync.requestFocus || request.requestFocus,
      preserveBottom: pendingViewportSync.preserveBottom && request.preserveBottom
    };
  };

  const cancelViewportSyncTimer = () => {
    if (viewportSyncTimer) {
      window.clearTimeout(viewportSyncTimer);
      viewportSyncTimer = 0;
    }
  };

  const isViewportNearBottom = () => {
    const buffer = terminal && terminal.buffer && terminal.buffer.active;
    if (!buffer) {
      return true;
    }

    return Math.abs(buffer.baseY - buffer.viewportY) <= 1;
  };

  const executeViewportSync = request => {
    if (!root || root.clientWidth === 0 || root.clientHeight === 0) {
      return;
    }

    const shouldStickBottom = request.preserveBottom && isViewportNearBottom();
    fitAddon.fit();

    if (shouldStickBottom) {
      terminal.scrollToBottom();
    }

    terminal.refresh(0, Math.max(terminal.rows - 1, 0));

    if (request.requestFocus && !isComposing) {
      terminal.focus();
      hasTerminalFocus = true;
    }
  };

  const flushViewportSync = () => {
    if (isComposing || !pendingViewportSync) {
      return;
    }

    const request = pendingViewportSync;
    pendingViewportSync = null;

    window.requestAnimationFrame(() => {
      executeViewportSync(request);
    });
  };

  const scheduleViewportSync = (rawRequest, delay) => {
    mergeViewportSyncRequest(normalizeViewportSyncRequest(rawRequest));
    cancelViewportSyncTimer();

    if (isComposing) {
      return;
    }

    viewportSyncTimer = window.setTimeout(() => {
      viewportSyncTimer = 0;
      flushViewportSync();
    }, delay);
  };

  terminal.loadAddon(fitAddon);
  terminal.open(root);
  applyThemeVariables(terminal.options.theme);
  terminal.writeln('FastCli terminal ready. Run an embedded command to start an interactive shell.');
  scheduleViewportSync({ reason: 'init', requestFocus: false, preserveBottom: true }, 0);

  terminal.onData(data => {
    postMessage({ type: 'input', data });
  });

  terminal.onResize(size => {
    postMessage({ type: 'resize', cols: size.cols, rows: size.rows });
  });

  new ResizeObserver(() => {
    scheduleViewportSync({ reason: 'container-resize', requestFocus: false, preserveBottom: true }, 16);
  }).observe(root);

  window.addEventListener('resize', () => {
    scheduleViewportSync({ reason: 'window-resize', requestFocus: false, preserveBottom: true }, 60);
  });

  window.addEventListener('focus', () => {
    scheduleViewportSync({ reason: 'window-focus', requestFocus: false, preserveBottom: true }, 0);
  });

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      scheduleViewportSync({ reason: 'visibility-visible', requestFocus: false, preserveBottom: true }, 0);
    }
  });

  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(() => {
      scheduleViewportSync({ reason: 'fonts-ready', requestFocus: false, preserveBottom: true }, 0);
    }).catch(() => {
    });
  }

  if (terminal.textarea) {
    terminal.textarea.addEventListener('focus', () => {
      hasTerminalFocus = true;
      scheduleViewportSync({ reason: 'textarea-focus', requestFocus: false, preserveBottom: true }, 0);
    });

    terminal.textarea.addEventListener('blur', () => {
      hasTerminalFocus = false;
    });

    terminal.textarea.addEventListener('compositionstart', () => {
      isComposing = true;
      cancelViewportSyncTimer();
    });

    terminal.textarea.addEventListener('compositionend', () => {
      isComposing = false;
      if (pendingViewportSync) {
        flushViewportSync();
      } else if (hasTerminalFocus) {
        scheduleViewportSync({ reason: 'composition-end', requestFocus: false, preserveBottom: true }, 0);
      }
    });
  }

  window.fastCliTerminal = {
    focus() {
      terminal.focus();
      hasTerminalFocus = true;
    },
    write(data) {
      if (typeof data === 'string' && data.length > 0) {
        terminal.write(data);
      }
    },
    syncViewport(options) {
      scheduleViewportSync(options, 0);
    },
    replace(data) {
      terminal.reset();
      if (typeof data === 'string' && data.length > 0) {
        terminal.write(data);
      }
      scheduleViewportSync({ reason: 'replace', requestFocus: false, preserveBottom: true }, 0);
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

      applyThemeVariables(terminal.options.theme);
      document.body.style.background = theme.background;
      document.body.style.color = theme.foreground;
      scheduleViewportSync({ reason: 'theme-change', requestFocus: false, preserveBottom: true }, 0);
    }
  };

  postMessage({ type: 'ready' });
})();
