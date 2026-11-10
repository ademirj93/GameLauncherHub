using System.IO;
using System.Reflection;
using System.Text.Json;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Catalog;

/// <summary>
/// Provedor oficial do catálogo de plataformas de jogos suportadas pelo Hub.
/// Carrega dados de catálogo via JSON configurável e suporta extensões de launchers pelo usuário.
/// </summary>
public class CatalogService : ICatalogService
{
    private readonly ILogger<CatalogService>? _logger;
    private readonly List<LauncherPlatform> _catalog = new();
    private readonly string _customCatalogPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CatalogService(ILogger<CatalogService>? logger = null)
    {
        _logger = logger;
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncherHub");
        _customCatalogPath = Path.Combine(appFolder, "custom_catalog.json");

        LoadCatalog();
    }

    public Task<IReadOnlyList<LauncherPlatform>> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        lock (_catalog)
        {
            return Task.FromResult<IReadOnlyList<LauncherPlatform>>(_catalog.ToList().AsReadOnly());
        }
    }

    public LauncherPlatform? GetById(string id)
    {
        lock (_catalog)
        {
            return _catalog.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }

    public async Task AddOrUpdateCustomPlatformAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        lock (_catalog)
        {
            var existingIndex = _catalog.FindIndex(p => string.Equals(p.Id, platform.Id, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                _catalog[existingIndex] = platform;
            }
            else
            {
                _catalog.Add(platform);
            }
        }

        await SaveCustomCatalogAsync(cancellationToken);
    }

    public async Task SetCustomExecutablePathAsync(string platformId, string executablePath, CancellationToken cancellationToken = default)
    {
        LauncherPlatform? target;
        lock (_catalog)
        {
            target = _catalog.FirstOrDefault(p => string.Equals(p.Id, platformId, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.DetectedExecutablePath = executablePath;
                target.State = LauncherState.INSTALLED;
                target.StatusMessage = "Configurado manualmente";
            }
        }

        if (target != null)
        {
            await SaveCustomCatalogAsync(cancellationToken);
        }
    }

    public async Task RemoveCustomPlatformAsync(string platformId, CancellationToken cancellationToken = default)
    {
        lock (_catalog)
        {
            _catalog.RemoveAll(p => string.Equals(p.Id, platformId, StringComparison.OrdinalIgnoreCase) && p.IsCustom);
        }

        await SaveCustomCatalogAsync(cancellationToken);
    }

    private async Task SaveCustomCatalogAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dir = Path.GetDirectoryName(_customCatalogPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            List<LauncherPlatform> toSave;
            lock (_catalog)
            {
                // Salva plataformas marcadas como IsCustom ou que tenham caminho manual alterado
                toSave = _catalog.Where(p => p.IsCustom || !string.IsNullOrWhiteSpace(p.DetectedExecutablePath)).ToList();
            }

            var json = JsonSerializer.Serialize(toSave, JsonOptions);
            await File.WriteAllTextAsync(_customCatalogPath, json, cancellationToken);
            _logger?.LogInformation("Catálogo customizado salvo com sucesso em {Path}.", _customCatalogPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao salvar catálogo customizado em {Path}.", _customCatalogPath);
        }
    }

    private void LoadCatalog()
    {
        lock (_catalog)
        {
            _catalog.Clear();

            // 1. Carrega o catálogo base embutido / arquivo JSON local
            var basePlatforms = LoadBaseCatalogFromJson();
            if (basePlatforms.Count > 0)
            {
                _catalog.AddRange(basePlatforms);
            }
            else
            {
                _catalog.AddRange(GetDefaultFallbackPlatforms());
            }

            // 2. Carrega extensões customizadas do usuário em %LOCALAPPDATA%\GameLauncherHub\custom_catalog.json
            LoadCustomCatalogOverrides();

            // 3. Aplica regras padrão de estado inicial
            foreach (var platform in _catalog)
            {
                if (!platform.IsSupportedOnWindows)
                {
                    platform.State = LauncherState.UNSUPPORTED;
                    platform.StatusMessage = platform.UnsupportedReason ?? "Incompatível com Windows";
                }
                else if (platform.State == LauncherState.UNKNOWN)
                {
                    platform.StatusMessage = "Aguardando verificação";
                }
            }
        }
    }

    private List<LauncherPlatform> LoadBaseCatalogFromJson()
    {
        try
        {
            // Tenta carregar primeiro do arquivo local próximo ao executável
            var localJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Catalog", "catalog.json");
            if (File.Exists(localJsonPath))
            {
                var json = File.ReadAllText(localJsonPath);
                var items = JsonSerializer.Deserialize<List<LauncherPlatform>>(json, JsonOptions);
                if (items != null && items.Count > 0)
                {
                    _logger?.LogInformation("Catálogo carregado do arquivo local {Path} com {Count} plataformas.", localJsonPath, items.Count);
                    return items;
                }
            }

            // Se não encontrou no diretório local, tenta carregar do EmbeddedResource no assembly
            var assembly = typeof(CatalogService).Assembly;
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("catalog.json", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(resourceName))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var reader = new StreamReader(stream);
                    var json = reader.ReadToEnd();
                    var items = JsonSerializer.Deserialize<List<LauncherPlatform>>(json, JsonOptions);
                    if (items != null && items.Count > 0)
                    {
                        _logger?.LogInformation("Catálogo carregado de recurso embutido ({Resource}) com {Count} plataformas.", resourceName, items.Count);
                        return items;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao carregar catálogo JSON. Utilizando fallback em memória.");
        }

        return new List<LauncherPlatform>();
    }

    private void LoadCustomCatalogOverrides()
    {
        try
        {
            if (File.Exists(_customCatalogPath))
            {
                var customJson = File.ReadAllText(_customCatalogPath);
                var customPlatforms = JsonSerializer.Deserialize<List<LauncherPlatform>>(customJson, JsonOptions);
                if (customPlatforms != null)
                {
                    foreach (var custom in customPlatforms)
                    {
                        var existingIndex = _catalog.FindIndex(p => string.Equals(p.Id, custom.Id, StringComparison.OrdinalIgnoreCase));
                        if (existingIndex >= 0)
                        {
                            _catalog[existingIndex] = custom;
                        }
                        else
                        {
                            _catalog.Add(custom);
                        }
                    }
                    _logger?.LogInformation("Extensões do catálogo customizado carregadas de {Path}.", _customCatalogPath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Falha ao carregar extensões de catálogo em {Path}.", _customCatalogPath);
        }
    }

    private static List<LauncherPlatform> GetDefaultFallbackPlatforms()
    {
        // Fallback rígido para garantir que a aplicação nunca fique vazia mesmo em ambientes sem I/O
        return new List<LauncherPlatform>
        {
            new()
            {
                Id = "steam",
                Name = "Steam",
                Tagline = "Valve Corporation",
                Description = "A principal plataforma global de distribuição de jogos para PC.",
                OfficialWebsite = "https://store.steampowered.com",
                DownloadPageUrl = "https://store.steampowered.com/about/",
                InstallerDirectUrl = "https://cdn.cloudflare.steamstatic.com/client/installer/SteamSetup.exe",
                ExpectedPublisher = "Valve",
                ExpectedExecutableName = "steam.exe",
                BrandColor = "#171A21",
                AccentColor = "#66C0F4",
                IconKey = "IconSteam"
            },
            new()
            {
                Id = "epic",
                Name = "Epic Games Launcher",
                Tagline = "Epic Games, Inc.",
                Description = "Acesso à loja Epic Games, jogos gratuitos semanais e Unreal Engine.",
                OfficialWebsite = "https://store.epicgames.com",
                DownloadPageUrl = "https://store.epicgames.com/download",
                ExpectedPublisher = "Epic Games Inc.",
                ExpectedExecutableName = "EpicGamesLauncher.exe",
                BrandColor = "#121212",
                AccentColor = "#0078F2",
                IconKey = "IconEpic"
            },
            new()
            {
                Id = "amazon",
                Name = "Amazon Games",
                Tagline = "Amazon Services LLC",
                Description = "Instale e gerencie os jogos resgatados no Prime Gaming.",
                OfficialWebsite = "https://gaming.amazon.com",
                DownloadPageUrl = "https://gaming.amazon.com/amazon-games-app",
                InstallerDirectUrl = "https://download.amazongames.com/AmazonGamesSetup.exe",
                ExpectedPublisher = "Amazon Services LLC",
                ExpectedExecutableName = "Amazon Games.exe",
                BrandColor = "#1E1A29",
                AccentColor = "#9D4EDD",
                IconKey = "IconAmazon"
            },
            new()
            {
                Id = "battlenet",
                Name = "Battle.net",
                Tagline = "Blizzard Entertainment",
                Description = "Plataforma para títulos da Blizzard e Activision.",
                OfficialWebsite = "https://battle.net",
                DownloadPageUrl = "https://battle.net/download/getInstaller?os=win&app=battle.net",
                ExpectedPublisher = "Blizzard Entertainment, Inc.",
                ExpectedExecutableName = "Battle.net.exe",
                BrandColor = "#0E1726",
                AccentColor = "#00AEFF",
                IconKey = "IconBattleNet"
            },
            new()
            {
                Id = "ubisoft",
                Name = "Ubisoft Connect",
                Tagline = "Ubisoft Entertainment",
                Description = "Ecossistema para jogos da Ubisoft.",
                OfficialWebsite = "https://ubisoftconnect.com",
                DownloadPageUrl = "https://ubisoftconnect.com",
                ExpectedPublisher = "Ubisoft",
                ExpectedExecutableName = "UbisoftConnect.exe",
                BrandColor = "#0B1526",
                AccentColor = "#0070FF",
                IconKey = "IconUbisoft"
            },
            new()
            {
                Id = "ea",
                Name = "EA app",
                Tagline = "Electronic Arts",
                Description = "Nova experiência rápida para jogos da EA.",
                OfficialWebsite = "https://www.ea.com/ea-app",
                DownloadPageUrl = "https://www.ea.com/ea-app",
                ExpectedPublisher = "Electronic Arts, Inc.",
                ExpectedExecutableName = "EADesktop.exe",
                BrandColor = "#1F1214",
                AccentColor = "#FF4747",
                IconKey = "IconEA"
            },
            new()
            {
                Id = "heroic",
                Name = "Heroic Games Launcher",
                Tagline = "Heroic Open Source",
                Description = "Cliente livre para Epic Games, GOG e Prime Gaming.",
                OfficialWebsite = "https://heroicgameslauncher.com",
                DownloadPageUrl = "https://heroicgameslauncher.com/downloads",
                ExpectedPublisher = "Heroic Games Launcher",
                ExpectedExecutableName = "Heroic.exe",
                BrandColor = "#1E122C",
                AccentColor = "#A855F7",
                IconKey = "IconHeroic"
            },
            new()
            {
                Id = "gog",
                Name = "GOG GALAXY",
                Tagline = "CD PROJEKT",
                Description = "Biblioteca de jogos DRM-free com conquistas e amigos unificados.",
                OfficialWebsite = "https://www.gog.com/galaxy",
                DownloadPageUrl = "https://www.gog.com/galaxy",
                ExpectedPublisher = "GOG sp. z o.o.",
                ExpectedExecutableName = "GalaxyClient.exe",
                BrandColor = "#1D132B",
                AccentColor = "#9333EA",
                IconKey = "IconGOG"
            },
            new()
            {
                Id = "playnite",
                Name = "Playnite",
                Tagline = "Josef Nemec Open Source",
                Description = "Centralizador open source com suporte a temas e emuladores.",
                OfficialWebsite = "https://playnite.link",
                DownloadPageUrl = "https://playnite.link",
                ExpectedPublisher = "Josef Nemec",
                ExpectedExecutableName = "Playnite.DesktopApp.exe",
                BrandColor = "#241712",
                AccentColor = "#EA580C",
                IconKey = "IconPlaynite"
            },
            new()
            {
                Id = "xbox",
                Name = "Xbox App",
                Tagline = "Microsoft Corporation",
                Description = "Aplicativo oficial da Microsoft para PC Game Pass e jogos Windows.",
                OfficialWebsite = "https://www.xbox.com/apps/xbox-app-for-pc",
                DownloadPageUrl = "https://www.microsoft.com/store/productId/9MV0B5HZVK9Z",
                IsWindowsStorePackage = true,
                PackageFamilyName = "Microsoft.GamingApp_8wekyb3d8bbwe",
                ExpectedPublisher = "Microsoft Corporation",
                ExpectedExecutableName = "XboxPcAppCE.exe",
                BrandColor = "#0D2114",
                AccentColor = "#107C10",
                IconKey = "IconXbox",
                Execution = new ExecutionRules
                {
                    ProtocolScheme = "xbox:"
                }
            },
            new()
            {
                Id = "recomplauncher",
                Name = "RecompLauncher",
                Tagline = "Ruben Ripalda Open Source",
                Description = "Launcher open-source para ports nativos e recompilações de console para PC.",
                OfficialWebsite = "https://recomplauncher.com/",
                DownloadPageUrl = "https://github.com/elripalda/RecompLauncher/releases",
                InstallerDirectUrl = "https://recomplauncher.com/api/launcher-beta.php?a=download",
                ExpectedPublisher = "Ruben Ripalda",
                ExpectedExecutableName = "recomplauncher.exe",
                RequiresDigitalSignature = true,
                IsOpenSource = true,
                BrandColor = "#0E1A2B",
                AccentColor = "#34F2E8",
                IconKey = "IconRecomp",
                Detection = new DetectionRules
                {
                    RegistryPaths = new()
                    {
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\RecompLauncher",
                        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\RecompLauncher"
                    },
                    KnownExecutableNames = new() { "recomplauncher.exe", "RecompLauncher.exe" },
                    DefaultInstallDirectories = new()
                    {
                        @"%LOCALAPPDATA%\RecompLauncher",
                        @"%LOCALAPPDATA%\Programs\RecompLauncher",
                        @"C:\Program Files\RecompLauncher"
                    },
                    StartMenuShortcutNames = new() { "RecompLauncher.lnk", "Recomp Launcher.lnk" },
                    AppPathNames = new() { "recomplauncher.exe", "RecompLauncher.exe" }
                },
                Installation = new InstallationRules
                {
                    SupportsSilentInstall = true,
                    SilentInstallArguments = "/S",
                    ExpectedInstallerExtension = ".exe"
                }
            }
        };
    }
}
