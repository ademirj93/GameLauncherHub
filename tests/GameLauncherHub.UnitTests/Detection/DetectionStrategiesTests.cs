using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Detection;
using Xunit;

namespace GameLauncherHub.UnitTests.Detection;

public class DetectionStrategiesTests
{
    [Fact]
    public async Task ProcessDetectionStrategy_WithNoExecutableNames_ShouldFailGracefully()
    {
        var strategy = new ProcessDetectionStrategy();
        var platform = new LauncherPlatform
        {
            Id = "dummy",
            Name = "Dummy Platform",
            Description = "Test",
            OfficialWebsite = "https://example.com",
            DownloadPageUrl = "https://example.com"
        };

        var result = await strategy.DetectAsync(platform);

        Assert.False(result.Success);
        Assert.Equal("ActiveProcess", result.StrategyUsed);
    }

    [Fact]
    public async Task WindowsStorePackageDetectionStrategy_ForStandardExe_ShouldReturnFailed()
    {
        var strategy = new WindowsStorePackageDetectionStrategy();
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Standard Exe Platform",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com",
            IsWindowsStorePackage = false
        };

        var result = await strategy.DetectAsync(platform);

        Assert.False(result.Success);
        Assert.Equal("WindowsStorePackage", result.StrategyUsed);
    }

    [Fact]
    public async Task KnownPathsDetectionStrategy_WithNonExistentPath_ShouldReturnFailed()
    {
        var strategy = new KnownPathsDetectionStrategy();
        var platform = new LauncherPlatform
        {
            Id = "fake",
            Name = "Fake Launcher",
            Description = "Testing",
            OfficialWebsite = "https://example.com",
            DownloadPageUrl = "https://example.com"
        };
        platform.Detection.DefaultInstallDirectories.Add(@"C:\NonExistentDirectory_12345");
        platform.Detection.KnownExecutableNames.Add("nonexistent.exe");

        var result = await strategy.DetectAsync(platform);

        Assert.False(result.Success);
        Assert.Equal("KnownInstallPaths", result.StrategyUsed);
    }
}

