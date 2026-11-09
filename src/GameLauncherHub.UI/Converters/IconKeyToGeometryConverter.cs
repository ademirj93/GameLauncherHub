using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte a chave textual do ícone (ex: "IconSteam", "IconEpic") no recurso Geometry correspondente definido em Icons.xaml.
/// </summary>
public class IconKeyToGeometryConverter : IValueConverter
{
    public static readonly IconKeyToGeometryConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string key && !string.IsNullOrWhiteSpace(key))
        {
            try
            {
                if (System.Windows.Application.Current != null && System.Windows.Application.Current.TryFindResource(key) is Geometry geometry)
                {
                    return geometry;
                }
            }
            catch
            {
                // Silencioso em caso de falha de recurso
            }
        }

        // Fallback para o ícone padrão de Play
        return System.Windows.Application.Current?.TryFindResource("IconPlay") as Geometry;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

