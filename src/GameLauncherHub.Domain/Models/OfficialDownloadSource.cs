namespace GameLauncherHub.Domain.Models;

/// <summary>
/// Informações canônicas e centralizadas de fontes de download oficiais para uma plataforma.
/// </summary>
public class OfficialDownloadSource
{
    public required string PlatformId { get; init; }
    public required string PlatformName { get; init; }
    public required string OfficialWebsite { get; init; }
    public required string OfficialDownloadPageUrl { get; init; }
    public string? DirectInstallerUrl { get; init; }
    public string? StoreProtocol { get; init; }
    public required string ExpectedPublisher { get; init; }
    public string ExpectedFileExtension { get; init; } = ".exe";
    public bool RequiresDigitalSignature { get; init; } = true;
    public bool IsOpenSource { get; init; } = false;
    public bool SupportsSilentInstall { get; init; } = false;
    public string? SilentInstallArguments { get; init; }
    public string[] WhitelistedDomains { get; init; } = Array.Empty<string>();
    public string Description { get; init; } = string.Empty;
}

