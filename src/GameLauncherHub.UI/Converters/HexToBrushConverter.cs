using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte código hexadecimal (#RRGGBB) em SolidColorBrush seguro do WPF.
/// </summary>
public class HexToBrushConverter : IValueConverter
{
    public static readonly HexToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string hex && !string.IsNullOrWhiteSpace(hex))
        {
            try
            {
                var color = (Color)ColorConverter.ConvertFromString(hex);
                return new SolidColorBrush(color);
            }
            catch
            {
                // Fallback silencioso
            }
        }

        return new SolidColorBrush(Color.FromRgb(30, 32, 38));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw freshNotImplementedException();
    }

    private static NotImplementedException freshNotImplementedException() => new();
}

