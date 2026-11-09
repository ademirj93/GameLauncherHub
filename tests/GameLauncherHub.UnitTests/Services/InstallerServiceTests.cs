using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class InstallerServiceTests
{
    private readonly InstallerService _installerService = new(NullLogger<InstallerService>.Instance);

    [Fact]
    public async Task InstallAsync_WhenFileDoesNotExist_ShouldReturnFailure()
    {
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Steam",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com"
        };

        var progress = new Progress<string>();
        var result = await _installerService.InstallAsync(platform, @"C:\NonExistent_Installer_123.exe", progress);

        Assert.False(result.Success);
        Assert.Contains("não encontrado", result.Message);
    }
}

