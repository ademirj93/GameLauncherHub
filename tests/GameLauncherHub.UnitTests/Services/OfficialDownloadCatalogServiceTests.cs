using GameLauncherHub.Infrastructure.Catalog;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class OfficialDownloadCatalogServiceTests
{
    private readonly OfficialDownloadCatalogService _catalogService = new();

    [Fact]
    public void Catalog_ShouldContainExactly11OfficialPlatforms()
    {
        var all = _catalogService.GetAllSources();

        Assert.Equal(11, all.Count);
        Assert.DoesNotContain(all, s => s.PlatformId == "lutris");
        Assert.Contains(all, s => s.PlatformId == "recomplauncher");
    }

    [Fact]
    public void RecompLauncher_MustHaveCorrectDirectUrlAndPublisher()
    {
        var recomp = _catalogService.GetSourceByPlatformId("recomplauncher");

        Assert.NotNull(recomp);
        Assert.Equal("https://recomplauncher.com/api/launcher-beta.php?a=download", recomp.DirectInstallerUrl);
        Assert.Equal("Ruben Ripalda", recomp.ExpectedPublisher);
        Assert.True(recomp.SupportsSilentInstall);
        Assert.Equal("/S", recomp.SilentInstallArguments);
        Assert.Contains("recomplauncher.com", recomp.WhitelistedDomains);
        Assert.Contains("github.com", recomp.WhitelistedDomains);
        Assert.Contains("release-assets.githubusercontent.com", recomp.WhitelistedDomains);
    }

    [Fact]
    public void AmazonGames_MustHaveDirectInstallerUrl()
    {
        var amazon = _catalogService.GetSourceByPlatformId("amazon");

        Assert.NotNull(amazon);
        Assert.Equal("https://download.amazongames.com/AmazonGamesSetup.exe", amazon.DirectInstallerUrl);
        Assert.Equal("Amazon.com Services LLC", amazon.ExpectedPublisher);
        Assert.Contains("download.amazongames.com", amazon.WhitelistedDomains);
    }

    [Fact]
    public void GOG_MustHaveDirectInstallerUrlAndValidWhitelistedDomains()
    {
        var gog = _catalogService.GetSourceByPlatformId("gog");

        Assert.NotNull(gog);
        Assert.Equal("https://webinstallers.gog-statics.com/download/GOG_Galaxy_2.0.exe", gog.DirectInstallerUrl);
        Assert.Equal("GOG sp. z o.o.", gog.ExpectedPublisher);
        Assert.Contains("gog-statics.com", gog.WhitelistedDomains);
        Assert.Contains("webinstallers.gog-statics.com", gog.WhitelistedDomains);
        Assert.Contains("content-system.gog.com", gog.WhitelistedDomains);
    }

    [Fact]
    public void EpicGames_MustHaveDirectInstallerUrlAndAkamaiWhitelistedDomains()
    {
        var epic = _catalogService.GetSourceByPlatformId("epic");

        Assert.NotNull(epic);
        Assert.Contains("EpicGamesLauncherInstaller.msi", epic.DirectInstallerUrl);
        Assert.Equal("Epic Games Inc.", epic.ExpectedPublisher);
        Assert.Contains("akamaized.net", epic.WhitelistedDomains);
        Assert.Contains("epicgames-download1.akamaized.net", epic.WhitelistedDomains);
        Assert.Contains("epicgamescdn.com", epic.WhitelistedDomains);
    }

    [Fact]
    public void BattleNet_MustHaveDirectInstallerUrlAndAkamaiWhitelistedDomains()
    {
        var battlenet = _catalogService.GetSourceByPlatformId("battlenet");

        Assert.NotNull(battlenet);
        Assert.Contains("Battle.net-Setup.exe", battlenet.DirectInstallerUrl);
        Assert.Equal("Blizzard Entertainment, Inc.", battlenet.ExpectedPublisher);
        Assert.Contains("akamaihd.net", battlenet.WhitelistedDomains);
        Assert.Contains("akamaized.net", battlenet.WhitelistedDomains);
        Assert.Contains("blzddist1-a.akamaihd.net", battlenet.WhitelistedDomains);
    }

    [Theory]
    [InlineData("amazon", "download.amazongames.com", true)]
    [InlineData("amazon", "arcus-uswest.amazon.com", true)]
    [InlineData("amazon", "luna.amazon.com", false)]
    [InlineData("amazon", "malicious-site.com", false)]
    [InlineData("steam", "cdn.cloudflare.steamstatic.com", true)]
    [InlineData("steam", "fake-steam.com", false)]
    [InlineData("gog", "webinstallers.gog-statics.com", true)]
    [InlineData("gog", "cdn.gog.com", true)]
    [InlineData("gog", "content-system.gog.com", true)]
    [InlineData("epic", "epicgames-download1.akamaized.net", true)]
    [InlineData("epic", "launcher-public-service-prod06.ol.epicgames.com", true)]
    [InlineData("battlenet", "blzddist1-a.akamaihd.net", true)]
    [InlineData("battlenet", "us.cdn.blizzard.com", true)]
    public void IsDomainAuthorized_ShouldAccuratelyValidateDomains(string platformId, string domain, bool expected)
    {
        bool isAuthorized = _catalogService.IsDomainAuthorized(platformId, domain);
        Assert.Equal(expected, isAuthorized);
    }

    [Fact]
    public void OpenSourcePlatforms_MustHaveAppropriateSignatureRules()
    {
        var playnite = _catalogService.GetSourceByPlatformId("playnite");
        Assert.NotNull(playnite);
        Assert.Equal("SignPath Foundation", playnite.ExpectedPublisher);
        Assert.True(playnite.IsOpenSource);

        var heroic = _catalogService.GetSourceByPlatformId("heroic");
        Assert.NotNull(heroic);
        Assert.False(heroic.RequiresDigitalSignature);
        Assert.True(heroic.IsOpenSource);
    }
}

