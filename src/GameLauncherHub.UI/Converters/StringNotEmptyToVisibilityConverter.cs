using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte uma string (como caminho de imagem/arte) em Visibility (Visible se preenchida, Collapsed se nula/vazia).
/// Aceita 'Inverse' no ConverterParameter para inverter o comportamento (útil para fallbacks).
/// </summary>
public class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNotEmpty = value is string str && !string.IsNullOrWhiteSpace(str);
        bool inverse = parameter is string paramStr && string.Equals(paramStr, "Inverse", StringComparison.OrdinalIgnoreCase);

        if (inverse)
        {
            return isNotEmpty ? Visibility.Collapsed : Visibility.Visible;
        }

        return isNotEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

