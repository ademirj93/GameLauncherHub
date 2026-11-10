using GameLauncherHub.Domain.Enums;

namespace GameLauncherHub.Domain.Models;

/// <summary>
/// Regras específicas de detecção para um launcher no Windows.
/// </summary>
public class DetectionRules
{
    public List<string> RegistryPaths { get; init; } = new();
    public List<string> KnownExecutableNames { get; init; } = new();
    public List<string> DefaultInstallDirectories { get; init; } = new();
    public List<string> ProtocolSchemes { get; init; } = new();
    public List<string> StartMenuShortcutNames { get; init; } = new();
    public List<string> AppPathNames { get; init; } = new();
}

/// <summary>
/// Regras específicas para execução do launcher.
/// </summary>
public class ExecutionRules
{
    public string? ProtocolScheme { get; init; }
    public string? DefaultArguments { get; init; }
    public bool PreferProtocolLaunch { get; init; } = false;
}

/// <summary>
/// Regras específicas para download e instalação do launcher.
/// </summary>
public class InstallationRules
{
    public bool SupportsSilentInstall { get; init; } = false;
    public string? SilentInstallArguments { get; init; }
    public string ExpectedInstallerExtension { get; init; } = ".exe";
    public string? OfficialDownloadRedirectorUrl { get; init; }
}

/// <summary>
/// Representa a definição e o estado em tempo de execução de uma plataforma de jogos no catálogo.
/// </summary>
public class LauncherPlatform
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public string Tagline { get; init; } = string.Empty;

    // Identidade Visual
    public string BrandColor { get; init; } = "#1E2026";
    public string AccentColor { get; init; } = "#3B82F6";
    public string SecondaryColor { get; init; } = "#18191E";
    public string IconKey { get; init; } = string.Empty;

    // Fontes Oficiais e Segurança
    public required string OfficialWebsite { get; init; }
    public required string DownloadPageUrl { get; init; }
    public string? InstallerDirectUrl { get; init; }
    public string? ExpectedPublisher { get; init; }
    public string? ExpectedExecutableName { get; init; }
    public bool RequiresDigitalSignature { get; init; } = true;
    public bool IsOpenSource { get; init; } = false;

    // Compatibilidade e Tipo
    public string Category { get; set; } = "Launcher"; // "Launcher" ou "Emulator"
    public string? TargetSystem { get; set; } // ex: "Nintendo Wii U", "Sony PlayStation 2", etc.
    public bool IsCustom { get; set; } = false;
    public bool IsSupportedOnWindows { get; init; } = true;
    public string? UnsupportedReason { get; init; }
    public bool IsWindowsStorePackage { get; init; } = false;
    public string? PackageFamilyName { get; init; }

    // Regras Estruturadas
    public DetectionRules Detection { get; init; } = new();
    public ExecutionRules Execution { get; init; } = new();
    public InstallationRules Installation { get; init; } = new();

    // Propriedades Legadas/Convenientes de Acesso Rápido
    public List<string> RegistryPaths => Detection.RegistryPaths;
    public List<string> KnownExecutableNames => Detection.KnownExecutableNames;
    public List<string> DefaultInstallDirectories => Detection.DefaultInstallDirectories;
    public string? ProtocolScheme => Execution.ProtocolScheme ?? (Detection.ProtocolSchemes.Count > 0 ? Detection.ProtocolSchemes[0] : null);
    public string? ExecutionArguments => Execution.DefaultArguments;
    public string? SilentInstallArguments => Installation.SilentInstallArguments;
    public bool SupportsSilentInstall => Installation.SupportsSilentInstall;

    // Estado em Tempo de Execução
    public LauncherState State { get; set; } = LauncherState.UNKNOWN;
    public string? DetectedVersion { get; set; }
    public string? DetectedExecutablePath { get; set; }
    public string? StatusMessage { get; set; }
    public double OperationProgress { get; set; }
    public string? OperationSpeed { get; set; }
}
