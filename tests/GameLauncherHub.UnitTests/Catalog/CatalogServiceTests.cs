using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Infrastructure.Catalog;
using Xunit;

namespace GameLauncherHub.UnitTests.Catalog;

public class CatalogServiceTests
{
    private readonly CatalogService _catalogService = new();

    [Fact]
    public async Task GetCatalogAsync_ShouldReturnConfiguredPlatforms()
    {
        var catalog = await _catalogService.GetCatalogAsync();

        Assert.NotNull(catalog);
        Assert.True(catalog.Count >= 32);
        Assert.Contains(catalog, p => p.Category == "Emulator");
        Assert.Contains(catalog, p => p.Category == "Launcher");
    }

    [Theory]
    [InlineData("steam", "Steam")]
    [InlineData("epic", "Epic Games Launcher")]
    [InlineData("amazon", "Amazon Games")]
    [InlineData("battlenet", "Battle.net")]
    [InlineData("ubisoft", "Ubisoft Connect")]
    [InlineData("ea", "EA app")]
    [InlineData("heroic", "Heroic Games Launcher")]
    [InlineData("gog", "GOG GALAXY")]
    [InlineData("playnite", "Playnite")]
    [InlineData("xbox", "Xbox App")]
    [InlineData("recomplauncher", "RecompLauncher")]
    [InlineData("cemu", "Cemu")]
    [InlineData("dolphin", "Dolphin")]
    [InlineData("snes9x", "Snes9x / rasSnes9x")]
    [InlineData("ppsspp", "PPSSPP")]
    [InlineData("duckstation", "DuckStation")]
    [InlineData("pcsx2", "PCSX2")]
    [InlineData("rpcs3", "RPCS3")]
    [InlineData("xenia", "Xenia")]
    [InlineData("xemu", "Xemu")]
    [InlineData("citron", "Citron Neo")]
    [InlineData("shadps4", "shadPS4")]
    public void GetById_ShouldFindConfiguredPlatforms(string id, string expectedName)
    {
        var platform = _catalogService.GetById(id);

        Assert.NotNull(platform);
        Assert.Equal(expectedName, platform.Name);
        Assert.False(string.IsNullOrWhiteSpace(platform.OfficialWebsite));
        Assert.False(string.IsNullOrWhiteSpace(platform.BrandColor));
        Assert.False(string.IsNullOrWhiteSpace(platform.AccentColor));
    }

    [Fact]
    public void Lutris_ShouldNotBePresentInWindowsDistribution()
    {
        var lutris = _catalogService.GetById("lutris");
        Assert.Null(lutris);
    }

    [Fact]
    public void Xbox_ShouldBeMarkedAsWindowsStorePackage()
    {
        var xbox = _catalogService.GetById("xbox");

        Assert.NotNull(xbox);
        Assert.True(xbox.IsWindowsStorePackage);
        Assert.False(string.IsNullOrWhiteSpace(xbox.PackageFamilyName));
    }
}
