using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Services;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v0.12.0", 0, 12, 0, 0)]
    [InlineData("0.10.11", 0, 10, 11, 0)]
    [InlineData("0.11.10b3", 0, 11, 10, 3)]
    [InlineData("v0.11.10b3", 0, 11, 10, 3)]
    [InlineData("11.10b3", 0, 11, 10, 3)]
    [InlineData("v11.10b3", 0, 11, 10, 3)]
    [InlineData("0.11.10-b3", 0, 11, 10, 3)]
    [InlineData("0.11.10.3", 0, 11, 10, 3)]
    [InlineData("0.11.10b2", 0, 11, 10, 2)]
    [InlineData("v0.11.10b2", 0, 11, 10, 2)]
    [InlineData("0.11.4", 0, 11, 4, 0)]
    [InlineData("v1.0.0.5", 1, 0, 0, 5)]
    [InlineData("release-2.5.1", 2, 5, 1, 0)]
    [InlineData("v1.0", 1, 0, 0, 0)]
    [InlineData("v11.10b4", 0, 11, 10, 4)]
    [InlineData("0.11.11b1", 0, 11, 11, 1)]
    [InlineData("v0.11.11b1", 0, 11, 11, 1)]
    [InlineData("11.11b1", 0, 11, 11, 1)]
    [InlineData("v11.11b1", 0, 11, 11, 1)]
    [InlineData("0.11.11-b1", 0, 11, 11, 1)]
    [InlineData("0.11.11.1", 0, 11, 11, 1)]
    public void TryParseSemanticVersion_ValidStrings_ShouldParseCorrectly(
        string rawTag, int expectedMajor, int expectedMinor, int expectedBuild, int expectedRevision)
    {
        bool success = GitHubUpdateService.TryParseSemanticVersion(rawTag, out var version);

        Assert.True(success);
        Assert.Equal(expectedMajor, version.Major);
        Assert.Equal(expectedMinor, version.Minor);
        Assert.Equal(expectedBuild, version.Build);
        Assert.Equal(expectedRevision, version.Revision);
    }

    [Fact]
    public void ReleaseVersionEquivalence_ShouldPreventFalsePositiveUpdate()
    {
        // Tag do GitHub (v11.10b3) e versão instalada (0.11.10b3) devem ser avaliadas como equivalentes
        bool okRemote = GitHubUpdateService.TryParseSemanticVersion("v11.10b3", out var remoteVersion);
        bool okLocal = GitHubUpdateService.TryParseSemanticVersion("0.11.10b3", out var localVersion);

        Assert.True(okRemote);
        Assert.True(okLocal);
        Assert.Equal(remoteVersion, localVersion);
        Assert.False(remoteVersion > localVersion, "A mesma versão não deve acusar atualização disponível.");

        // Futura release v11.10b4 deve disparar atualização
        Assert.True(GitHubUpdateService.TryParseSemanticVersion("v11.10b4", out var futureBeta));
        Assert.True(futureBeta > localVersion, "v11.10b4 deve ser superior a 0.11.10b3.");

        // Versão anterior 0.11.10b2 deve reconhecer que 11.10b3 é mais recente
        Assert.True(GitHubUpdateService.TryParseSemanticVersion("0.11.10b2", out var olderBeta));
        Assert.True(remoteVersion > olderBeta, "v11.10b3 deve ser superior a 0.11.10b2.");

        // Release 0.11.11b1 deve disparar atualização em relação a 0.11.10b3
        Assert.True(GitHubUpdateService.TryParseSemanticVersion("v0.11.11b1", out var v1111b1Remote));
        Assert.True(GitHubUpdateService.TryParseSemanticVersion("0.11.11b1", out var v1111b1Local));
        Assert.Equal(v1111b1Remote, v1111b1Local);
        Assert.True(v1111b1Remote > localVersion, "0.11.11b1 deve ser superior a 0.11.10b3.");
        Assert.False(v1111b1Remote > v1111b1Local, "A mesma versão 0.11.11b1 não deve acusar atualização.");

        // Release oficial 0.11.11 deve disparar atualização
        Assert.True(GitHubUpdateService.TryParseSemanticVersion("v0.11.11", out var nextMinor));
        Assert.True(nextMinor > localVersion, "0.11.11 deve ser superior a 0.11.10b3.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-tag")]
    [InlineData("beta")]
    public void TryParseSemanticVersion_InvalidStrings_ShouldReturnFalse(string invalidTag)
    {
        bool success = GitHubUpdateService.TryParseSemanticVersion(invalidTag, out var version);

        Assert.False(success);
        Assert.Equal(new Version(0, 0, 0), version);
    }

    [Fact]
    public void VersionComparison_ShouldCorrectlyIdentifyNewerVersion()
    {
        var current = new Version(0, 11, 4);
        var newer = new Version(0, 12, 0);
        var same = new Version(0, 11, 4);
        var older = new Version(0, 11, 3);

        Assert.True(newer > current);
        Assert.False(same > current);
        Assert.False(older > current);
    }

    [Fact]
    public void AppUpdateInfo_FormattedSize_ShouldFormatCorrectly()
    {
        var info = new AppUpdateInfo
        {
            InstallerSizeBytes = 52428800 // 50 MB
        };

        Assert.Equal("50.0 MB", info.FormattedSize);
    }
}

