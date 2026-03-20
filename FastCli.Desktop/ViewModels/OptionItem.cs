namespace FastCli.Desktop.ViewModels;

public sealed class OptionItem<T>
{
    public required T Value { get; init; }

    public required string Label { get; init; }
}
