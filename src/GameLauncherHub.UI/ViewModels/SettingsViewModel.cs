using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Security;
using GameLauncherHub.Infrastructure.Services;
using Microsoft.Win32;

namespace GameLauncherHub.UI.ViewModels;

/// <summary>
/// ViewModel da janela de configurações e preferências da aplicação.
/// Gerencia armazenamento, System Tray, integração segura SteamGridDB e visualização.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly ISteamGridDBService _steamGridDbService;
    private readonly IArtworkCacheService _artworkCacheService;
    private readonly ILauncherArtworkService _launcherArtworkService;
    private readonly ILauncherOrchestratorService _orchestratorService;

    [ObservableProperty]
    private string _downloadDirectory = string.Empty;

    [ObservableProperty]
    private bool _autoCleanupInstallers = true;

    [ObservableProperty]
    private bool _launchOnStartup;

    [ObservableProperty]
    private bool _enableSystemTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private string _trayDisplayMode = "AllInstalled";

    [ObservableProperty]
    private bool _isTrayAllInstalled = true;

    [ObservableProperty]
    private bool _isTrayFavoritesOnly;

    partial void OnIsTrayAllInstalledChanged(bool value)
    {
        if (value)
        {
            TrayDisplayMode = "AllInstalled";
            if (_isTrayFavoritesOnly) IsTrayFavoritesOnly = false;
        }
    }

    partial void OnIsTrayFavoritesOnlyChanged(bool value)
    {
        if (value)
        {
            TrayDisplayMode = "FavoritesOnly";
            if (_isTrayAllInstalled) IsTrayAllInstalled = false;
        }
    }

    [ObservableProperty]
    private string _viewMode = "Cards";

    [ObservableProperty]
    private bool _isViewCards = true;

    [ObservableProperty]
    private bool _isViewCompact;

    partial void OnIsViewCardsChanged(bool value)
    {
        if (value)
        {
            ViewMode = "Cards";
            if (_isViewCompact) IsViewCompact = false;
        }
    }

    partial void OnIsViewCompactChanged(bool value)
    {
        if (value)
        {
            ViewMode = "Compact";
            if (_isViewCards) IsViewCards = false;
        }
    }

    [ObservableProperty]
    private bool _autoCheckInstallationsOnStartup = true;

    [ObservableProperty]
    private bool _autoCheckUpdatesOnStartup = true;

    [ObservableProperty]
    private string _theme = "Dark";

    [ObservableProperty]
    private string _language = "pt-BR";

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private string _steamGridDbApiKey = string.Empty;

    [ObservableProperty]
    private string? _apiTestStatus;

    [ObservableProperty]
    private bool _isTestingApiKey;

    [ObservableProperty]
    private string? _statusMessage;

    public event Action? RequestClose;

    public SettingsViewModel(
        ISettingsService settingsService,
        ISteamGridDBService steamGridDbService,
        IArtworkCacheService artworkCacheService,
        ILauncherArtworkService launcherArtworkService,
        ILauncherOrchestratorService orchestratorService)
    {
        _settingsService = settingsService;
        _steamGridDbService = steamGridDbService;
        _artworkCacheService = artworkCacheService;
        _launcherArtworkService = launcherArtworkService;
        _orchestratorService = orchestratorService;
    }

    public async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        DownloadDirectory = settings.DownloadDirectory;
        AutoCleanupInstallers = settings.AutoCleanupInstallers;
        LaunchOnStartup = WindowsStartupManager.IsLaunchOnStartupEnabled();
        EnableSystemTray = settings.EnableSystemTray;
        CloseToTray = settings.CloseToTray;
        MinimizeToTray = settings.MinimizeToTray;
        TrayDisplayMode = string.IsNullOrWhiteSpace(settings.TrayDisplayMode) ? "AllInstalled" : settings.TrayDisplayMode;
        IsTrayFavoritesOnly = string.Equals(TrayDisplayMode, "FavoritesOnly", StringComparison.OrdinalIgnoreCase);
        IsTrayAllInstalled = !IsTrayFavoritesOnly;

        ViewMode = string.IsNullOrWhiteSpace(settings.ViewMode) ? "Cards" : settings.ViewMode;
        IsViewCompact = string.Equals(ViewMode, "Compact", StringComparison.OrdinalIgnoreCase);
        IsViewCards = !IsViewCompact;
        AutoCheckInstallationsOnStartup = settings.AutoCheckInstallationsOnStartup;
        AutoCheckUpdatesOnStartup = settings.AutoCheckUpdatesOnStartup;
        Theme = settings.Theme;
        Language = settings.Language;
        ShowNotifications = settings.ShowNotifications;

        if (!string.IsNullOrWhiteSpace(settings.SteamGridDbApiKeyProtected))
        {
            SteamGridDbApiKey = DataProtectionHelper.UnprotectString(settings.SteamGridDbApiKeyProtected);
        }
    }

    [RelayCommand]
    private async Task TestSteamGridDbApiKeyAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            ApiTestStatus = "Por favor, insira uma API Key antes de testar.";
            return;
        }

        IsTestingApiKey = true;
        ApiTestStatus = "Testando conexão com a API v2 do SteamGridDB...";

        try
        {
            bool isValid = await _steamGridDbService.ValidateApiKeyAsync(SteamGridDbApiKey.Trim());
            ApiTestStatus = isValid
                ? "✓ Conexão bem-sucedida! API Key válida e autenticada."
                : "✗ Chave rejeitada pela API do SteamGridDB (Não autorizada ou inválida).";
        }
        catch (Exception ex)
        {
            ApiTestStatus = $"✗ Erro de comunicação: {ex.Message}";
        }
        finally
        {
            IsTestingApiKey = false;
        }
    }

    [RelayCommand]
    private void ClearArtworkCache()
    {
        try
        {
            _artworkCacheService.ClearCache();
            ApiTestStatus = "✓ Cache local de imagens esvaziado com sucesso.";
        }
        catch (Exception ex)
        {
            ApiTestStatus = $"Erro ao limpar cache: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshArtworkAsync()
    {
        if (string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            ApiTestStatus = "Configure e teste sua API Key antes de atualizar as imagens.";
            return;
        }

        ApiTestStatus = "Atualizando imagens dos launchers em segundo plano...";
        try
        {
            var platforms = await _orchestratorService.LoadAndDetectAllAsync();
            await _launcherArtworkService.RefreshAllArtworksAsync(platforms);
            ApiTestStatus = "✓ Imagens atualizadas com sucesso a partir do SteamGridDB!";
        }
        catch (Exception ex)
        {
            ApiTestStatus = $"Falha ao atualizar imagens: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseDownloadDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar Pasta para Downloads dos Instaladores",
            InitialDirectory = DownloadDirectory
        };

        if (dialog.ShowDialog() == true)
        {
            DownloadDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();

        settings.DownloadDirectory = DownloadDirectory;
        settings.AutoCleanupInstallers = AutoCleanupInstallers;
        settings.LaunchOnStartup = LaunchOnStartup;
        settings.EnableSystemTray = EnableSystemTray;
        settings.CloseToTray = CloseToTray;
        settings.MinimizeToTray = MinimizeToTray;
        settings.TrayDisplayMode = TrayDisplayMode;
        settings.ViewMode = ViewMode;
        settings.AutoCheckInstallationsOnStartup = AutoCheckInstallationsOnStartup;
        settings.AutoCheckUpdatesOnStartup = AutoCheckUpdatesOnStartup;
        settings.Theme = Theme;
        settings.Language = Language;
        settings.ShowNotifications = ShowNotifications;

        // Proteger API key via DPAPI antes de gravar
        if (!string.IsNullOrWhiteSpace(SteamGridDbApiKey))
        {
            settings.SteamGridDbApiKeyProtected = DataProtectionHelper.ProtectString(SteamGridDbApiKey.Trim());
        }
        else
        {
            settings.SteamGridDbApiKeyProtected = string.Empty;
        }

        await _settingsService.SaveSettingsAsync(settings);
        WindowsStartupManager.SetLaunchOnStartup(LaunchOnStartup);

        StatusMessage = "Configurações salvas com sucesso!";
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void ResetDefaults()
    {
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncherHub");
        DownloadDirectory = Path.Combine(appFolder, "Downloads");
        AutoCleanupInstallers = true;
        LaunchOnStartup = false;
        EnableSystemTray = true;
        CloseToTray = true;
        MinimizeToTray = true;
        TrayDisplayMode = "AllInstalled";
        IsTrayAllInstalled = true;
        IsTrayFavoritesOnly = false;
        ViewMode = "Cards";
        IsViewCards = true;
        IsViewCompact = false;
        AutoCheckInstallationsOnStartup = true;
        AutoCheckUpdatesOnStartup = true;
        Theme = "Dark";
        Language = "pt-BR";
        ShowNotifications = true;
        SteamGridDbApiKey = string.Empty;
        ApiTestStatus = null;
        StatusMessage = "Valores restaurados para os padrões.";
    }
}
