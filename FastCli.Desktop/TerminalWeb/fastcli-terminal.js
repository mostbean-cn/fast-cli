(function () {
  const root = document.getElementById('terminal-root');
  const applyThemeVariables = theme => {
    const styles = document.documentElement.style;
    styles.setProperty('--terminal-bg', theme.background);
    styles.setProperty('--terminal-fg', theme.foreground);
    styles.setProperty('--terminal-selection', theme.selectionBackground);
    styles.setProperty(
      '--terminal-scrollbar-thumb',
      theme.background.toLowerCase() === '#ffffff' ? 'rgba(107, 114, 128, 0.35)' : 'rgba(148, 163, 184, 0.45)');
  };
  const fitAddon = new FitAddon.FitAddon();
  let fitTimer = 0;
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

  const syncInputElements = () => {
    const core = terminal && terminal._core;
    if (!core) {
      return;
    }

    try {
      if (typeof core._syncTextArea === 'function') {
        core._syncTextArea();
      }

      if (core._compositionHelper && typeof core._compositionHelper.updateCompositionElements === 'function') {
        core._compositionHelper.updateCompositionElements();
      }
    } catch {
    }
  };

  const isViewportNearBottom = () => {
    const buffer = terminal && terminal.buffer && terminal.buffer.active;
    if (!buffer) {
      return true;
    }

    return Math.abs(buffer.baseY - buffer.viewportY) <= 1;
  };

  const keepViewportAtBottom = shouldStickBottom => {
    if (shouldStickBottom) {
      terminal.scrollToBottom();
    }
  };

  const fit = () => {
    if (!root || root.clientWidth === 0 || root.clientHeight === 0) {
      return;
    }

    const shouldStickBottom = isViewportNearBottom();
    fitAddon.fit();
    keepViewportAtBottom(shouldStickBottom);
    syncInputElements();
  };

  const fitDeferred = () => {
    window.requestAnimationFrame(() => {
      fit();
      window.requestAnimationFrame(() => {
        fit();
      });
    });
  };

  const scheduleFit = delay => {
    if (fitTimer) {
      window.clearTimeout(fitTimer);
    }

    fitTimer = window.setTimeout(() => {
      fitTimer = 0;
      fitDeferred();
    }, delay);
  };

  terminal.loadAddon(fitAddon);
  terminal.open(root);
  applyThemeVariables(terminal.options.theme);
  fitDeferred();
  terminal.focus();
  terminal.writeln('FastCli terminal ready. Run an embedded command to start an interactive shell.');

  terminal.onData(data => {
    postMessage({ type: 'input', data });
  });

  terminal.onCursorMove(() => {
    window.requestAnimationFrame(syncInputElements);
  });

  terminal.onResize(size => {
    postMessage({ type: 'resize', cols: size.cols, rows: size.rows });
    window.requestAnimationFrame(syncInputElements);
  });

  new ResizeObserver(() => {
    scheduleFit(16);
  }).observe(root);

  window.addEventListener('resize', () => {
    scheduleFit(60);
  });

  window.addEventListener('focus', () => {
    scheduleFit(0);
    window.requestAnimationFrame(syncInputElements);
  });

  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      scheduleFit(0);
      window.requestAnimationFrame(syncInputElements);
    }
  });

  if (document.fonts && document.fonts.ready) {
    document.fonts.ready.then(() => {
      scheduleFit(0);
    }).catch(() => {
    });
  }

  if (terminal.textarea) {
    ['focus', 'blur', 'input', 'compositionstart', 'compositionupdate', 'compositionend', 'keyup'].forEach(eventName => {
      terminal.textarea.addEventListener(eventName, () => {
        window.requestAnimationFrame(syncInputElements);
      });
    });
  }

  window.fastCliTerminal = {
    focus() {
      terminal.focus();
      syncInputElements();
    },
    write(data) {
      if (typeof data === 'string' && data.length > 0) {
        terminal.write(data);
      }
    },
    syncViewport() {
      const shouldStickBottom = isViewportNearBottom();
      const refresh = () => {
        fit();
        keepViewportAtBottom(shouldStickBottom);
        terminal.refresh(0, Math.max(terminal.rows - 1, 0));
        syncInputElements();
      };

      scheduleFit(0);
      window.setTimeout(refresh, 24);
      window.setTimeout(refresh, 72);
      window.setTimeout(refresh, 144);
      window.setTimeout(refresh, 260);
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

      applyThemeVariables(terminal.options.theme);
      document.body.style.background = theme.background;
      document.body.style.color = theme.foreground;
      window.fastCliTerminal.syncViewport();
    }
  };

  postMessage({ type: 'ready' });
})();
