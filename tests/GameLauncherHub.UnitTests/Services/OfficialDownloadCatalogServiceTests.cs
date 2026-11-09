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

    [Theory]
    [InlineData("amazon", "download.amazongames.com", true)]
    [InlineData("amazon", "arcus-uswest.amazon.com", true)]
    [InlineData("amazon", "luna.amazon.com", false)]
    [InlineData("amazon", "malicious-site.com", false)]
    [InlineData("steam", "cdn.cloudflare.steamstatic.com", true)]
    [InlineData("steam", "fake-steam.com", false)]
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

