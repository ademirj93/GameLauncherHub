using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GameLauncherHub.UI.Converters;

/// <summary>
/// Conversor de alto desempenho para imagens e artes dos launchers.
/// Decodifica bitmaps em resolução reduzida (thumbnails congelados) para economizar até 95% de memória RAM
/// em comparação ao carregamento padrão do WPF em resolução nativa.
/// </summary>
public class ThumbnailImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, BitmapImage> _cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        int decodeWidth = 64;
        if (parameter is string paramStr && int.TryParse(paramStr, out int customWidth) && customWidth > 0)
        {
            decodeWidth = customWidth;
        }

        string cacheKey = $"{path}_{decodeWidth}";
        return _cache.GetOrAdd(cacheKey, _ =>
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(path, UriKind.Absolute);
                bitmap.DecodePixelWidth = decodeWidth;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.DelayCreation;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null!;
            }
        });
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

