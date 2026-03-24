using System.Windows;
using FastCli.Desktop.Layout;
using Xunit;

namespace FastCli.Tests;

public sealed class TerminalPanelLayoutPresetTests
{
    [Fact]
    public void Closed_ResetsEditorLayoutAndCollapsesTerminalRows()
    {
        var preset = TerminalPanelLayoutPreset.Closed;

        Assert.Equal(new GridLength(1, GridUnitType.Star), preset.EditorRowHeight);
        Assert.Equal(new GridLength(0), preset.SplitterRowHeight);
        Assert.Equal(new GridLength(0), preset.TerminalRowHeight);
    }

    [Fact]
    public void Open_RestoresDefaultEditorAndTerminalSplit()
    {
        var preset = TerminalPanelLayoutPreset.Open;

        Assert.Equal(new GridLength(7, GridUnitType.Star), preset.EditorRowHeight);
        Assert.Equal(new GridLength(8), preset.SplitterRowHeight);
        Assert.Equal(new GridLength(3, GridUnitType.Star), preset.TerminalRowHeight);
    }
}
