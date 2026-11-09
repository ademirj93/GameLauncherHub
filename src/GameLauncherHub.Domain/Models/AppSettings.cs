namespace GameLauncherHub.Domain.Models;

/// <summary>
/// Preferências e configurações da aplicação armazenadas localmente.
/// </summary>
public class AppSettings
{
    // Armazenamento & Downloads
    public string DownloadDirectory { get; set; } = string.Empty;
    public bool AutoCleanupInstallers { get; set; } = true;

    // Inicialização & Sistema
    public bool LaunchOnStartup { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public bool EnableSystemTray { get; set; } = true;
    public string TrayDisplayMode { get; set; } = "AllInstalled"; // "AllInstalled" ou "FavoritesOnly"

    // Detecção & Comportamento
    public bool AutoCheckInstallationsOnStartup { get; set; } = true;
    public bool AutoCheckUpdatesOnStartup { get; set; } = true;
    public string GitHubRepoOwner { get; set; } = "ademirj93";
    public string GitHubRepoName { get; set; } = "GameLauncherHub";
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "pt-BR";
    public bool ShowNotifications { get; set; } = true;

    // Interface & Visualização
    public string ViewMode { get; set; } = "Cards"; // "Cards" ou "Compact"

    // Favoritos
    public List<string> FavoriteLauncherIds { get; set; } = new();

    // SteamGridDB (Chave de API protegida com DPAPI do Windows em Base64)
    public string SteamGridDbApiKeyProtected { get; set; } = string.Empty;
}

