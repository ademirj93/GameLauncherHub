using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GameLauncherHub.Domain.Enums;
using Color = System.Windows.Media.Color;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Converte o LauncherState no SolidColorBrush temático de destaque correspondente.
/// </summary>
public class StateToBrushConverter : IValueConverter
{
    public static readonly StateToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is LauncherState state)
        {
            return state switch
            {
                LauncherState.INSTALLED => new SolidColorBrush(Color.FromRgb(16, 185, 129)),    // Esmeralda / Sucesso
                LauncherState.RUNNING => new SolidColorBrush(Color.FromRgb(59, 130, 246)),      // Azul Elétrico / Ativo
                LauncherState.DOWNLOADING => new SolidColorBrush(Color.FromRgb(147, 51, 234)),  // Roxo / Processando
                LauncherState.INSTALLING => new SolidColorBrush(Color.FromRgb(234, 88, 12)),   // Laranja / Executando
                LauncherState.NOT_INSTALLED => new SolidColorBrush(Color.FromRgb(99, 102, 241)),// Índigo / Disponível
                LauncherState.UNSUPPORTED => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Âmbar / Incompatível
                LauncherState.ERROR => new SolidColorBrush(Color.FromRgb(239, 68, 68)),        // Vermelho / Erro
                _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))                          // Cinza neutro
            };
        }

        return new SolidColorBrush(Color.FromRgb(107, 114, 128));
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

