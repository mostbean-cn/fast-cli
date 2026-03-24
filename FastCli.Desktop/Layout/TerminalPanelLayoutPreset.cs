using System.Windows;

namespace FastCli.Desktop.Layout;

public readonly record struct TerminalPanelLayoutPreset(
    GridLength EditorRowHeight,
    GridLength SplitterRowHeight,
    GridLength TerminalRowHeight)
{
    public static TerminalPanelLayoutPreset Open { get; } = new(
        new GridLength(7, GridUnitType.Star),
        new GridLength(8),
        new GridLength(3, GridUnitType.Star));

    public static TerminalPanelLayoutPreset Closed { get; } = new(
        new GridLength(1, GridUnitType.Star),
        new GridLength(0),
        new GridLength(0));
}
