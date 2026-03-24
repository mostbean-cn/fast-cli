using System.Windows;

namespace FastCli.Desktop.Layout;

public readonly record struct TerminalPanelLayoutPreset(
    GridLength EditorRowHeight,
    GridLength SplitterRowHeight,
    GridLength TerminalRowHeight)
{
    public static TerminalPanelLayoutPreset Open { get; } = new(
        new GridLength(1, GridUnitType.Star),
        new GridLength(8),
        new GridLength(260));

    public static TerminalPanelLayoutPreset Closed { get; } = new(
        new GridLength(1, GridUnitType.Star),
        new GridLength(0),
        new GridLength(0));

    public static TerminalPanelLayoutPreset Maximized { get; } = new(
        new GridLength(0),
        new GridLength(0),
        new GridLength(1, GridUnitType.Star));
}
