using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Services;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class GitHubUpdateServiceTests
{
    [Theory]
    [InlineData("v0.12.0", 0, 12, 0)]
    [InlineData("0.10.11", 0, 10, 11)]
    [InlineData("0.11.10b2", 0, 11, 10)]
    [InlineData("v0.11.10b2", 0, 11, 10)]
    [InlineData("0.11.4", 0, 11, 4)]
    [InlineData("v1.0.0.5", 1, 0, 0)]
    [InlineData("release-2.5.1", 2, 5, 1)]
    [InlineData("v1.0", 1, 0, 0)]
    public void TryParseSemanticVersion_ValidStrings_ShouldParseCorrectly(
        string rawTag, int expectedMajor, int expectedMinor, int expectedBuild)
    {
        bool success = GitHubUpdateService.TryParseSemanticVersion(rawTag, out var version);

        Assert.True(success);
        Assert.Equal(expectedMajor, version.Major);
        Assert.Equal(expectedMinor, version.Minor);
        Assert.Equal(expectedBuild, version.Build);
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

