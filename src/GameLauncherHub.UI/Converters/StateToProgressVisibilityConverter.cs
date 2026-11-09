using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GameLauncherHub.Domain.Enums;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Exibe a barra de progresso apenas se o launcher estiver nos estados DOWNLOADING ou INSTALLING.
/// </summary>
public class StateToProgressVisibilityConverter : IValueConverter
{
    public static readonly StateToProgressVisibilityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LauncherState state)
        {
            return (state == LauncherState.DOWNLOADING || state == LauncherState.INSTALLING)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

