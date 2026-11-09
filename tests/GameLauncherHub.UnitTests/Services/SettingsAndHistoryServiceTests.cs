using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class SettingsAndHistoryServiceTests
{
    [Fact]
    public async Task SettingsService_LoadSettings_ShouldReturnValidDefaultPaths()
    {
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        var settings = await settingsService.LoadSettingsAsync();

        Assert.NotNull(settings);
        Assert.False(string.IsNullOrWhiteSpace(settings.DownloadDirectory));
        Assert.True(settings.AutoCleanupInstallers);
        Assert.True(settings.AutoCheckInstallationsOnStartup);
        Assert.Equal("Dark", settings.Theme);
    }

    [Fact]
    public async Task OperationHistoryService_LogAndRetrieve_ShouldPreserveRecords()
    {
        var historyService = new OperationHistoryService(NullLogger<OperationHistoryService>.Instance);
        await historyService.ClearHistoryAsync();

        var log = new OperationLog
        {
            LauncherId = "steam",
            LauncherName = "Steam",
            OperationType = OperationType.Download,
            Success = true,
            Message = "Download de teste concluído com sucesso."
        };

        await historyService.LogAsync(log);

        var recent = await historyService.GetRecentLogsAsync(10);
        Assert.NotEmpty(recent);
        Assert.Contains(recent, l => l.LauncherId == "steam" && l.OperationType == OperationType.Download);
    }

    [Fact]
    public async Task SettingsService_TrayDisplayMode_And_Events_ShouldPersistCorrectly()
    {
        var settingsService = new SettingsService(NullLogger<SettingsService>.Instance);
        var settings = await settingsService.LoadSettingsAsync();

        bool eventFired = false;
        settingsService.SettingsChanged += s =>
        {
            if (s.TrayDisplayMode == "FavoritesOnly")
            {
                eventFired = true;
            }
        };

        settings.TrayDisplayMode = "FavoritesOnly";
        await settingsService.SaveSettingsAsync(settings);

        Assert.Equal("FavoritesOnly", settingsService.CurrentSettings.TrayDisplayMode);
        Assert.True(eventFired);

        // Reverte para o estado padrão
        settings.TrayDisplayMode = "AllInstalled";
        await settingsService.SaveSettingsAsync(settings);
        Assert.Equal("AllInstalled", settingsService.CurrentSettings.TrayDisplayMode);
    }
}

