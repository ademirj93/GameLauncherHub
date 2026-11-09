using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Infrastructure.Catalog;
using Xunit;

namespace GameLauncherHub.UnitTests.Catalog;

public class CatalogLoaderTests
{
    private readonly CatalogService _catalogService = new();

    [Fact]
    public async Task Catalog_ShouldContainExactly11OfficialPlatforms()
    {
        var catalog = await _catalogService.GetCatalogAsync();

        Assert.NotNull(catalog);
        Assert.Equal(11, catalog.Count);

        var expectedIds = new[]
        {
            "steam", "epic", "amazon", "battlenet", "ubisoft",
            "ea", "heroic", "gog", "playnite", "xbox", "recomplauncher"
        };

        foreach (var id in expectedIds)
        {
            Assert.Contains(catalog, p => p.Id == id);
        }

        // Lutris não deve estar presente no catálogo do Windows
        Assert.DoesNotContain(catalog, p => p.Id == "lutris");
    }

    [Fact]
    public void Catalog_ShouldHaveRichDetectionRulesLoaded()
    {
        var steam = _catalogService.GetById("steam");
        Assert.NotNull(steam);
        Assert.Contains("steam.exe", steam.Detection.KnownExecutableNames);
        Assert.Contains("steam", steam.Detection.ProtocolSchemes);
        Assert.NotEmpty(steam.Detection.RegistryPaths);

        var epic = _catalogService.GetById("epic");
        Assert.NotNull(epic);
        Assert.Contains("EpicGamesLauncher.exe", epic.Detection.KnownExecutableNames);
        Assert.Contains("com.epicgames.launcher", epic.Detection.ProtocolSchemes);

        var xbox = _catalogService.GetById("xbox");
        Assert.NotNull(xbox);
        Assert.True(xbox.IsWindowsStorePackage);
        Assert.Equal("Microsoft.GamingApp_8wekyb3d8bbwe", xbox.PackageFamilyName);

        var lutris = _catalogService.GetById("lutris");
        Assert.Null(lutris);
    }
}
