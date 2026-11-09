using System.IO;
using System.Text.Json;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Serviço de persistência local de configurações no formato JSON em %LOCALAPPDATA%.
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private readonly string _settingsFilePath;
    private AppSettings _currentSettings;

    public AppSettings CurrentSettings => _currentSettings;
    public event Action<AppSettings>? SettingsChanged;

    public SettingsService(ILogger<SettingsService> logger)
    {
        _logger = logger;
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncherHub");
        Directory.CreateDirectory(appFolder);
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
        _currentSettings = LoadSettingsSynchronously();
    }

    private AppSettings LoadSettingsSynchronously()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null) return settings;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro na leitura síncrona de configurações locais de {Path}", _settingsFilePath);
        }

        var defaultSettings = new AppSettings
        {
            DownloadDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncherHub", "Downloads")
        };
        try { Directory.CreateDirectory(defaultSettings.DownloadDirectory); } catch { }
        return defaultSettings;
    }

    public async Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath, cancellationToken).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    _currentSettings = settings;
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao carregar configurações locais de {Path}", _settingsFilePath);
        }

        return _currentSettings;
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(settings, options);
            await File.WriteAllTextAsync(_settingsFilePath, json, cancellationToken).ConfigureAwait(false);
            _currentSettings = settings;
            SettingsChanged?.Invoke(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao salvar configurações em {Path}", _settingsFilePath);
        }
    }
}

