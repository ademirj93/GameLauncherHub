using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.Infrastructure.Catalog;

/// <summary>
/// Provedor centralizado com o catálogo oficial de fontes de download de cada plataforma.
/// Centraliza URLs, publishers e whitelists de domínios seguros.
/// </summary>
public class OfficialDownloadCatalogService : IOfficialDownloadCatalogService
{
    private static readonly Dictionary<string, OfficialDownloadSource> Sources = new(StringComparer.OrdinalIgnoreCase)
    {
        ["steam"] = new()
        {
            PlatformId = "steam",
            PlatformName = "Steam",
            OfficialWebsite = "https://store.steampowered.com",
            OfficialDownloadPageUrl = "https://store.steampowered.com/about/",
            DirectInstallerUrl = "https://cdn.cloudflare.steamstatic.com/client/installer/SteamSetup.exe",
            ExpectedPublisher = "Valve",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = true,
            SilentInstallArguments = "/S",
            WhitelistedDomains = new[] { "steampowered.com", "steamstatic.com", "steamcdn-a.akamaihd.net", "cloudflare.steamstatic.com" },
            Description = "Instalador oficial do cliente Steam da Valve Corporation."
        },
        ["epic"] = new()
        {
            PlatformId = "epic",
            PlatformName = "Epic Games Launcher",
            OfficialWebsite = "https://store.epicgames.com",
            OfficialDownloadPageUrl = "https://store.epicgames.com/download",
            DirectInstallerUrl = "https://launcher-public-service-prod06.ol.epicgames.com/launcher/api/installer/download/EpicGamesLauncherInstaller.msi",
            ExpectedPublisher = "Epic Games Inc.",
            ExpectedFileExtension = ".msi",
            SupportsSilentInstall = true,
            SilentInstallArguments = "/qn",
            WhitelistedDomains = new[] { "epicgames.com", "ol.epicgames.com" },
            Description = "Pacote MSI oficial do inicializador da Epic Games."
        },
        ["amazon"] = new()
        {
            PlatformId = "amazon",
            PlatformName = "Amazon Games",
            OfficialWebsite = "https://gaming.amazon.com",
            OfficialDownloadPageUrl = "https://gaming.amazon.com/amazon-games-app",
            DirectInstallerUrl = "https://download.amazongames.com/AmazonGamesSetup.exe",
            ExpectedPublisher = "Amazon.com Services LLC",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = false,
            WhitelistedDomains = new[] { "amazongames.com", "amazon.com", "download.amazongames.com" },
            Description = "Instalador executável direto oficial do Amazon Games App para Windows (não confundir com Amazon Luna)."
        },
        ["battlenet"] = new()
        {
            PlatformId = "battlenet",
            PlatformName = "Battle.net",
            OfficialWebsite = "https://battle.net",
            OfficialDownloadPageUrl = "https://battle.net/download/getInstaller?os=win&app=battle.net",
            DirectInstallerUrl = "https://battle.net/download/getInstaller?os=win&app=battle.net",
            ExpectedPublisher = "Blizzard Entertainment, Inc.",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = false,
            WhitelistedDomains = new[] { "battle.net", "blizzard.com" },
            Description = "Instalador oficial do ecossistema Blizzard Battle.net."
        },
        ["ubisoft"] = new()
        {
            PlatformId = "ubisoft",
            PlatformName = "Ubisoft Connect",
            OfficialWebsite = "https://ubisoftconnect.com",
            OfficialDownloadPageUrl = "https://ubisoftconnect.com",
            DirectInstallerUrl = "https://ubi.li/4vxt9",
            ExpectedPublisher = "Ubisoft",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = true,
            SilentInstallArguments = "/S",
            WhitelistedDomains = new[] { "ubisoftconnect.com", "ubi.li", "ubisoft.com", "static3.cdn.ubi.com" },
            Description = "Instalador oficial da Ubisoft Connect."
        },
        ["ea"] = new()
        {
            PlatformId = "ea",
            PlatformName = "EA app",
            OfficialWebsite = "https://www.ea.com/ea-app",
            OfficialDownloadPageUrl = "https://www.ea.com/ea-app",
            DirectInstallerUrl = "https://origin-a.akamaihd.net/EA-Desktop-Client-Download/installer-releases/EAappInstaller.exe",
            ExpectedPublisher = "Electronic Arts, Inc.",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = false,
            WhitelistedDomains = new[] { "ea.com", "origin.com", "akamaihd.net", "origin-a.akamaihd.net" },
            Description = "Instalador oficial da nova aplicação desktop da Electronic Arts."
        },
        ["heroic"] = new()
        {
            PlatformId = "heroic",
            PlatformName = "Heroic Games Launcher",
            OfficialWebsite = "https://heroicgameslauncher.com",
            OfficialDownloadPageUrl = "https://heroicgameslauncher.com/downloads",
            DirectInstallerUrl = "https://github.com/Heroic-Games-Launcher/HeroicGamesLauncher/releases/latest/download/Heroic-Setup-x64.exe",
            ExpectedPublisher = "Heroic Games Launcher",
            ExpectedFileExtension = ".exe",
            RequiresDigitalSignature = false,
            IsOpenSource = true,
            SupportsSilentInstall = true,
            SilentInstallArguments = "/S",
            WhitelistedDomains = new[] { "heroicgameslauncher.com", "github.com", "github-releases.githubusercontent.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com", "api.github.com" },
            Description = "Instalador oficial do cliente livre Heroic Games Launcher."
        },
        ["gog"] = new()
        {
            PlatformId = "gog",
            PlatformName = "GOG GALAXY",
            OfficialWebsite = "https://www.gog.com/galaxy",
            OfficialDownloadPageUrl = "https://www.gog.com/galaxy",
            DirectInstallerUrl = "https://cdn.gog.com/open/galaxy/client/2.0.73.27/galaxy_setup_2.0.73.27.exe",
            ExpectedPublisher = "GOG sp. z o.o.",
            ExpectedFileExtension = ".exe",
            SupportsSilentInstall = true,
            SilentInstallArguments = "/VERYSILENT /SUPPRESSMSGBOXES",
            WhitelistedDomains = new[] { "gog.com", "cdn.gog.com" },
            Description = "Instalador oficial do cliente DRM-free GOG GALAXY."
        },
        ["playnite"] = new()
        {
            PlatformId = "playnite",
            PlatformName = "Playnite",
            OfficialWebsite = "https://playnite.link",
            OfficialDownloadPageUrl = "https://playnite.link",
            DirectInstallerUrl = "https://playnite.link/download/PlayniteInstaller.exe",
            ExpectedPublisher = "SignPath Foundation",
            ExpectedFileExtension = ".exe",
            RequiresDigitalSignature = true,
            IsOpenSource = true,
            SupportsSilentInstall = true,
            SilentInstallArguments = "/SILENT",
            WhitelistedDomains = new[] { "playnite.link", "github.com", "github-releases.githubusercontent.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com" },
            Description = "Instalador oficial do agregador de jogos de código aberto Playnite."
        },
        ["xbox"] = new()
        {
            PlatformId = "xbox",
            PlatformName = "Xbox App",
            OfficialWebsite = "https://www.xbox.com/apps/xbox-app-for-pc",
            OfficialDownloadPageUrl = "https://www.microsoft.com/store/productId/9MV0B5HZVK9Z",
            DirectInstallerUrl = null,
            StoreProtocol = "ms-windows-store://pdp/?productid=9MV0B5HZVK9Z",
            ExpectedPublisher = "Microsoft Corporation",
            ExpectedFileExtension = ".msix",
            SupportsSilentInstall = false,
            WhitelistedDomains = new[] { "microsoft.com", "xbox.com" },
            Description = "Aplicativo oficial Xbox da Microsoft distribuído via Windows Store / MSIX."
        },
        ["recomplauncher"] = new()
        {
            PlatformId = "recomplauncher",
            PlatformName = "RecompLauncher",
            OfficialWebsite = "https://recomplauncher.com/",
            OfficialDownloadPageUrl = "https://github.com/elripalda/RecompLauncher/releases",
            DirectInstallerUrl = "https://recomplauncher.com/api/launcher-beta.php?a=download",
            ExpectedPublisher = "Ruben Ripalda",
            ExpectedFileExtension = ".exe",
            RequiresDigitalSignature = true,
            IsOpenSource = true,
            SupportsSilentInstall = true,
            SilentInstallArguments = "/S",
            WhitelistedDomains = new[] { "recomplauncher.com", "github.com", "githubusercontent.com", "release-assets.githubusercontent.com", "api.github.com" },
            Description = "Launcher oficial open-source para ports nativos e recompilações de consoles no PC."
        }
    };

    public IReadOnlyList<OfficialDownloadSource> GetAllSources()
    {
        return Sources.Values.ToList().AsReadOnly();
    }

    public OfficialDownloadSource? GetSourceByPlatformId(string platformId)
    {
        return Sources.TryGetValue(platformId, out var source) ? source : null;
    }

    public bool IsDomainAuthorized(string platformId, string domain)
    {
        if (Sources.TryGetValue(platformId, out var source))
        {
            // Bloqueio rigoroso contra domínios de cloud gaming/streaming incompatíveis
            if (string.Equals(platformId, "amazon", StringComparison.OrdinalIgnoreCase))
            {
                if (domain.Contains("luna", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return source.WhitelistedDomains.Any(w =>
                domain.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                domain.EndsWith("." + w, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }
}

