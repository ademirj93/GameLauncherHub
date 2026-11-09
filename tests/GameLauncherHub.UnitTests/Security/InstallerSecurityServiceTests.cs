using System.IO;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameLauncherHub.UnitTests.Security;

public class InstallerSecurityServiceTests
{
    private readonly InstallerSecurityService _securityService = new(NullLogger<InstallerSecurityService>.Instance);

    [Theory]
    [InlineData("http://store.steampowered.com/SteamSetup.exe")]
    [InlineData("http://cdn.cloudflare.steamstatic.com/SteamSetup.exe")]
    public void ValidateDownloadUrl_WhenNotHttps_ShouldReject(string insecureUrl)
    {
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Test",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com"
        };

        bool isValid = _securityService.ValidateDownloadUrl(insecureUrl, platform, out var reason);

        Assert.False(isValid);
        Assert.Contains("HTTPS", reason);
    }

    [Theory]
    [InlineData("https://unauthorized-mirror.net/SteamSetup.exe")]
    [InlineData("https://random-cdn.com/EpicInstaller.msi")]
    [InlineData("https://pirate-downloads.org/ea.exe")]
    public void ValidateDownloadUrl_WhenDomainNotAuthorized_ShouldReject(string unauthorizedUrl)
    {
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Test",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com"
        };

        bool isValid = _securityService.ValidateDownloadUrl(unauthorizedUrl, platform, out var reason);

        Assert.False(isValid);
        Assert.Contains("não consta na lista de fornecedores oficiais", reason);
    }

    [Theory]
    [InlineData("https://cdn.cloudflare.steamstatic.com/client/installer/SteamSetup.exe")]
    [InlineData("https://store.steampowered.com/about/")]
    public void ValidateDownloadUrl_WhenOfficialDomain_ShouldApprove(string officialUrl)
    {
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Test",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com"
        };

        bool isValid = _securityService.ValidateDownloadUrl(officialUrl, platform, out var reason);

        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Fact]
    public async Task ValidateInstallerFileAsync_WhenFileDoesNotExist_ShouldReturnInvalid()
    {
        var platform = new LauncherPlatform
        {
            Id = "steam",
            Name = "Steam",
            Description = "Test",
            OfficialWebsite = "https://store.steampowered.com",
            DownloadPageUrl = "https://store.steampowered.com"
        };

        var result = await _securityService.ValidateInstallerFileAsync(@"C:\NonExistent_File_123.exe", platform);

        Assert.False(result.IsValid);
        Assert.Contains("não foi encontrado", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateInstallerFileAsync_WhenFileIsNotValidPEHeader_ShouldReject()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            // Escreve mais de 100KB de texto simulado sem cabeçalho PE "MZ"
            var dummyData = new byte[150 * 1024];
            Array.Fill(dummyData, (byte)'A');
            await File.WriteAllBytesAsync(tempFile, dummyData);

            var platform = new LauncherPlatform
            {
                Id = "steam",
                Name = "Steam",
                Description = "Test",
                OfficialWebsite = "https://store.steampowered.com",
                DownloadPageUrl = "https://store.steampowered.com"
            };

            var result = await _securityService.ValidateInstallerFileAsync(tempFile, platform);

            Assert.False(result.IsValid);
            Assert.Contains("cabeçalho PE/MSI ausente", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Theory]
    [InlineData("Amazon.com Services LLC", "Amazon Services LLC", true)]
    [InlineData("Amazon Services LLC", "Amazon.com Services LLC", true)]
    [InlineData("Amazon.com Services LLC", "Amazon", true)]
    [InlineData("Epic Games, Inc.", "Epic Games Inc.", true)]
    [InlineData("Epic Games, Inc.", "Epic Games", true)]
    [InlineData("Valve Corp.", "Valve", true)]
    [InlineData("Open Source Developer Ruben Ripalda", "Ruben Ripalda", true)]
    [InlineData("Blizzard Entertainment, Inc.", "Blizzard Entertainment, Inc.", true)]
    [InlineData("Malicious Imposter", "Valve", false)]
    [InlineData("Random Publisher", "Epic Games", false)]
    public void IsPublisherMatch_ShouldValidateCorrectly(string actual, string expected, bool shouldMatch)
    {
        bool result = InstallerSecurityService.IsPublisherMatch(actual, expected);
        Assert.Equal(shouldMatch, result);
    }

    [Fact]
    public async Task ValidateInstallerFileAsync_WhenOpenSourceWithoutSignature_ShouldAcceptWithOfficialOrigin()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        try
        {
            var dummyData = new byte[150 * 1024];
            dummyData[0] = 0x4D; // M
            dummyData[1] = 0x5A; // Z
            await File.WriteAllBytesAsync(tempFile, dummyData);

            var platform = new LauncherPlatform
            {
                Id = "heroic",
                Name = "Heroic Games Launcher",
                Description = "Open Source Launcher",
                OfficialWebsite = "https://heroicgameslauncher.com",
                DownloadPageUrl = "https://heroicgameslauncher.com/downloads",
                RequiresDigitalSignature = false,
                IsOpenSource = true
            };

            var result = await _securityService.ValidateInstallerFileAsync(tempFile, platform);

            Assert.True(result.IsValid);
            Assert.False(result.IsAuthenticodeSigned);
            Assert.Equal("[Código Aberto]", result.SubjectPublisher);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

