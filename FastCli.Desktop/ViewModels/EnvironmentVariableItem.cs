using FastCli.Desktop.Mvvm;

namespace FastCli.Desktop.ViewModels;

public sealed class EnvironmentVariableItem : ObservableObject
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    public string Key
    {
        get => _key;
        set => SetProperty(ref _key, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }
}
