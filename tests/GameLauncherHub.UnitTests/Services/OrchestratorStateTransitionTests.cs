using GameLauncherHub.Application.Services;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class OrchestratorStateTransitionTests
{
    private class MockDetectionService : ILauncherDetectionService
    {
        public Func<LauncherPlatform, DetectionResult>? DetectionHandler { get; set; }

        public Task<DetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            var result = DetectionHandler?.Invoke(platform) ?? new DetectionResult(false, null, null, false, null);
            return Task.FromResult(result);
        }

        public Task<bool> IsRunningAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }

    private class MockExecutionService : ILauncherExecutionService
    {
        public Task<ExecutionResult> LaunchAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ExecutionResult(true, 1234, "Sucesso"));
        }
    }

    private class MockCatalogService : ICatalogService
    {
        public Task<IReadOnlyList<LauncherPlatform>> GetCatalogAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LauncherPlatform>>(new List<LauncherPlatform>());
        }

        public LauncherPlatform? GetById(string id) => null;
        public Task AddOrUpdateCustomPlatformAsync(LauncherPlatform platform, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SetCustomExecutablePathAsync(string platformId, string executablePath, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveCustomPlatformAsync(string platformId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class MockHistoryService : IOperationHistoryService
    {
        public Task LogAsync(OperationLog log, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<OperationLog>> GetRecentLogsAsync(int count = 50, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<OperationLog>>(new List<OperationLog>());
        public Task ClearHistoryAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class MockDownloadManager : IDownloadManager
    {
        public Task<string> DownloadInstallerAsync(string downloadUrl, string targetFileName, IProgress<DownloadProgressInfo> progress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(targetFileName);
        }
    }

    private class MockSecurityService : IInstallerSecurityService
    {
        public bool ValidateDownloadUrl(string url, LauncherPlatform platform, out string? failureReason)
        {
            failureReason = null;
            return true;
        }

        public Task<SecurityValidationResult> ValidateInstallerFileAsync(string filePath, LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SecurityValidationResult(true, true, "Valve", "sha256", "OK", null));
        }
    }

    private class MockInstallerService : IInstallerService
    {
        public Task<InstallationResult> InstallAsync(LauncherPlatform platform, string installerFilePath, IProgress<string> statusProgress, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new InstallationResult(true, "OK"));
        }
    }

    private class MockSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings => new AppSettings { DownloadDirectory = @"C:\Temp" };
        public event Action<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CurrentSettings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private LauncherOrchestratorService CreateOrchestrator(ILauncherDetectionService detectionService)
    {
        return new LauncherOrchestratorService(
            NullLogger<LauncherOrchestratorService>.Instance,
            new MockCatalogService(),
            detectionService,
            new MockExecutionService(),
            new MockHistoryService(),
            new MockDownloadManager(),
            new MockSecurityService(),
            new MockInstallerService(),
            new MockSettingsService()
        );
    }

    [Fact]
    public async Task RefreshPlatformStatus_WhenRunning_ShouldTransitionToRunningState()
    {
        var mockDetection = new MockDetectionService
        {
            DetectionHandler = _ => new DetectionResult(
                IsInstalled: true,
                ExecutablePath: @"C:\Games\steam.exe",
                InstalledVersion: "1.0.0",
                IsRunning: true,
                Notes: "Em execução"
            )
        };

        var orchestrator = CreateOrchestrator(mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Valve Steam",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com",
            IsSupportedOnWindows = true
        };

        await orchestrator.RefreshPlatformStatusAsync(platform);

        Assert.Equal(LauncherState.RUNNING, platform.State);
        Assert.Equal(@"C:\Games\steam.exe", platform.DetectedExecutablePath);
        Assert.Equal("1.0.0", platform.DetectedVersion);
    }

    [Fact]
    public async Task RefreshPlatformStatus_WhenInstalledNotRunning_ShouldTransitionToInstalledState()
    {
        var mockDetection = new MockDetectionService
        {
            DetectionHandler = _ => new DetectionResult(
                IsInstalled: true,
                ExecutablePath: @"C:\Games\steam.exe",
                InstalledVersion: "2.5.0",
                IsRunning: false,
                Notes: "Instalado"
            )
        };

        var orchestrator = CreateOrchestrator(mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Valve Steam",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com",
            IsSupportedOnWindows = true
        };

        await orchestrator.RefreshPlatformStatusAsync(platform);

        Assert.Equal(LauncherState.INSTALLED, platform.State);
        Assert.Equal("2.5.0", platform.DetectedVersion);
    }

    [Fact]
    public async Task RefreshPlatformStatus_WhenNotInstalled_ShouldTransitionToNotInstalledState()
    {
        var mockDetection = new MockDetectionService
        {
            DetectionHandler = _ => new DetectionResult(
                IsInstalled: false,
                ExecutablePath: null,
                InstalledVersion: null,
                IsRunning: false,
                Notes: "Não encontrado"
            )
        };

        var orchestrator = CreateOrchestrator(mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "epic",
            Name = "Epic Games Launcher",
            Description = "Epic Games",
            OfficialWebsite = "https://store.epicgames.com",
            DownloadPageUrl = "https://store.epicgames.com",
            IsSupportedOnWindows = true
        };

        await orchestrator.RefreshPlatformStatusAsync(platform);

        Assert.Equal(LauncherState.NOT_INSTALLED, platform.State);
    }

    [Fact]
    public async Task RefreshPlatformStatus_WhenUnsupported_ShouldKeepUnsupportedStateWithoutCallingDetection()
    {
        bool detectionCalled = false;
        var mockDetection = new MockDetectionService
        {
            DetectionHandler = _ =>
            {
                detectionCalled = true;
                return new DetectionResult(false, null, null, false, null);
            }
        };

        var orchestrator = CreateOrchestrator(mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "lutris",
            Name = "Lutris",
            Description = "Linux Gaming",
            OfficialWebsite = "https://lutris.net",
            DownloadPageUrl = "https://lutris.net",
            IsSupportedOnWindows = false,
            UnsupportedReason = "Apenas Linux"
        };

        await orchestrator.RefreshPlatformStatusAsync(platform);

        Assert.Equal(LauncherState.UNSUPPORTED, platform.State);
        Assert.False(detectionCalled);
    }
}
