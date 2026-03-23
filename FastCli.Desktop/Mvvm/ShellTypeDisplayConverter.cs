using System.Globalization;
using System.Windows.Data;
using FastCli.Domain.Enums;

namespace FastCli.Desktop.Mvvm;

public sealed class ShellTypeDisplayConverter : IValueConverter, IMultiValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ShellType shellType
            ? EnumDisplayHelper.ToDisplayText(shellType)
            : value?.ToString() ?? string.Empty;
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        return values.Length > 0
            ? Convert(values[0], targetType, parameter, culture)
            : string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
