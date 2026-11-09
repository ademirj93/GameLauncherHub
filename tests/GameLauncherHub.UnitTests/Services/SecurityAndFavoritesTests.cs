using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using GameLauncherHub.Infrastructure.Favorites;
using GameLauncherHub.Infrastructure.Security;
using Xunit;

namespace GameLauncherHub.UnitTests.Services;

public class SecurityAndFavoritesTests
{
    private class FakeSettingsService : ISettingsService
    {
        public AppSettings Settings { get; set; } = new();
        public AppSettings CurrentSettings => Settings;
        public event Action<AppSettings>? SettingsChanged;

        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Settings);
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = settings;
            SettingsChanged?.Invoke(settings);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void DataProtectionHelper_RoundTrip_ShouldEncryptAndDecryptAccurately()
    {
        string secretApiKey = "sgdb_test_api_key_1234567890abcdef";

        string protectedData = DataProtectionHelper.ProtectString(secretApiKey);
        Assert.False(string.IsNullOrWhiteSpace(protectedData));
        Assert.NotEqual(secretApiKey, protectedData);

        string restoredKey = DataProtectionHelper.UnprotectString(protectedData);
        Assert.Equal(secretApiKey, restoredKey);
    }

    [Fact]
    public void DataProtectionHelper_EmptyOrNullString_ShouldReturnEmpty()
    {
        Assert.Equal(string.Empty, DataProtectionHelper.ProtectString(""));
        Assert.Equal(string.Empty, DataProtectionHelper.UnprotectString(""));
    }

    [Fact]
    public async Task LauncherFavoritesService_ToggleAndPersist_ShouldWorkProperly()
    {
        var fakeSettings = new FakeSettingsService();
        var favoritesService = new LauncherFavoritesService(fakeSettings);

        bool eventFired = false;
        favoritesService.FavoritesChanged += () => { eventFired = true; };

        // Inicialmente nenhum favorito
        Assert.False(favoritesService.IsFavorite("steam"));

        // Marcar como favorito
        bool isFav = await favoritesService.ToggleFavoriteAsync("steam");
        Assert.True(isFav);
        Assert.True(favoritesService.IsFavorite("steam"));
        Assert.True(eventFired);
        Assert.Contains("steam", fakeSettings.Settings.FavoriteLauncherIds);

        // Desmarcar favorito
        isFav = await favoritesService.ToggleFavoriteAsync("steam");
        Assert.False(isFav);
        Assert.False(favoritesService.IsFavorite("steam"));
        Assert.DoesNotContain("steam", fakeSettings.Settings.FavoriteLauncherIds);
    }

    [Fact]
    public void LauncherFavoritesService_ShouldInitializeWithFavoritesFromSettings_Synchronously()
    {
        var fakeSettings = new FakeSettingsService
        {
            Settings = new AppSettings
            {
                FavoriteLauncherIds = new List<string> { "steam", "epic", "recomplauncher" }
            }
        };

        var favoritesService = new LauncherFavoritesService(fakeSettings);

        // Deve carregar síncronamente logo no construtor
        Assert.True(favoritesService.IsFavorite("steam"));
        Assert.True(favoritesService.IsFavorite("epic"));
        Assert.True(favoritesService.IsFavorite("recomplauncher"));
        Assert.False(favoritesService.IsFavorite("gog"));
    }
}

