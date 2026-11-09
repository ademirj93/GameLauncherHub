using System.Globalization;
using System.Windows.Data;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Inverte um valor booleano no Data Binding do WPF.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public static readonly InverseBooleanConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }

        return false;
    }
}

