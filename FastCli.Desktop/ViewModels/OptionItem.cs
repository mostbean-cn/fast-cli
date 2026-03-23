using FastCli.Desktop.Mvvm;

namespace FastCli.Desktop.ViewModels;

public sealed class OptionItem<T> : ObservableObject
{
    private T? _value;
    private string _label = string.Empty;
    private string? _description;
    private string? _meta;

    public required T Value
    {
        get => _value!;
        init => _value = value;
    }

    public required string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string? Meta
    {
        get => _meta;
        set => SetProperty(ref _meta, value);
    }
}
