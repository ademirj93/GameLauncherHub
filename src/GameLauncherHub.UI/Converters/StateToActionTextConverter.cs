using System.Globalization;
using System.Windows.Data;
using GameLauncherHub.Domain.Enums;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte o LauncherState no texto descritivo do botão de ação principal.
/// </summary>
public class StateToActionTextConverter : IValueConverter
{
    public static readonly StateToActionTextConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LauncherState state)
        {
            return state switch
            {
                LauncherState.INSTALLED => "▶ Executar",
                LauncherState.RUNNING => "● Em Execução",
                LauncherState.DOWNLOADING => "⏳ Baixando...",
                LauncherState.INSTALLING => "⚙ Instalando...",
                LauncherState.NOT_INSTALLED => "↓ Instalar",
                LauncherState.UNSUPPORTED => "ℹ Incompatível",
                LauncherState.ERROR => "⚠ Tentar Novamente",
                _ => "Verificando..."
            };
        }

        return "Verificando...";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

