using System.Globalization;
using System.Windows.Data;
using GameLauncherHub.Domain.Enums;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte o LauncherState no texto exibido no badge de status do card.
/// </summary>
public class StateToBadgeTextConverter : IValueConverter
{
    public static readonly StateToBadgeTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LauncherState state)
        {
            return state switch
            {
                LauncherState.INSTALLED => "INSTALADO",
                LauncherState.RUNNING => "EM EXECUÇÃO",
                LauncherState.DOWNLOADING => "BAIXANDO",
                LauncherState.INSTALLING => "INSTALANDO",
                LauncherState.NOT_INSTALLED => "DISPONÍVEL",
                LauncherState.UNSUPPORTED => "APENAS LINUX",
                LauncherState.ERROR => "ERRO",
                _ => "DETECTANDO"
            };
        }

        return "DESCONHECIDO";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

