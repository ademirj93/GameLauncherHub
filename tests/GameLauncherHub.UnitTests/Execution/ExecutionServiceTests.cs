using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameLauncherHub.UnitTests.Execution;

public class ExecutionServiceTests
{
    private class MockDetectionService : ILauncherDetectionService
    {
        public bool IsRunningValue { get; set; } = false;

        public Task<DetectionResult> DetectAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new DetectionResult(false, null, null, IsRunningValue, null));
        }

        public Task<bool> IsRunningAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(IsRunningValue);
        }
    }

    [Fact]
    public async Task LaunchAsync_WhenPlatformIsUnsupported_ShouldReturnFailure()
    {
        var mockDetection = new MockDetectionService();
        var executionService = new LauncherExecutionService(NullLogger<LauncherExecutionService>.Instance, mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "lutris",
            Name = "Lutris",
            Description = "Linux Gaming",
            OfficialWebsite = "https://lutris.net",
            DownloadPageUrl = "https://lutris.net",
            IsSupportedOnWindows = false,
            UnsupportedReason = "Incompatível com Windows"
        };

        var result = await executionService.LaunchAsync(platform);

        Assert.False(result.Success);
        Assert.Contains("Incompatível com Windows", result.Message);
    }

    [Fact]
    public async Task LaunchAsync_WhenAlreadyRunningWithProtocol_ShouldReturnSuccess()
    {
        var mockDetection = new MockDetectionService { IsRunningValue = true };
        var executionService = new LauncherExecutionService(NullLogger<LauncherExecutionService>.Instance, mockDetection);

        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Steam",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com",
            IsSupportedOnWindows = true,
            Execution = new ExecutionRules { ProtocolScheme = "steam://" }
        };

        var result = await executionService.LaunchAsync(platform);

        Assert.True(result.Success);
        Assert.Contains("ativado", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}

