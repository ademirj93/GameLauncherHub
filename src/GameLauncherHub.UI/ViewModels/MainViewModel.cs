using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.UI.ViewModels;

/// <summary>
/// ViewModel principal da janela do GameLauncher Hub.
/// Gerencia catálogo, filtragem, visualização (Cards vs Compacta), favoritos e integração de artes.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private readonly ILauncherOrchestratorService _orchestratorService;
    private readonly ISettingsService _settingsService;
    private readonly ILauncherFavoritesService _favoritesService;
    private readonly ILauncherArtworkService _artworkService;
    private readonly List<LauncherCardViewModel> _allCards = new();

    public ObservableCollection<LauncherCardViewModel> FilteredCards { get; } = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFilter = "Todos";

    [ObservableProperty]
    private string _viewMode = "Cards"; // "Cards" ou "Compact"

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Pronto";

    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _installedCount;

    [ObservableProperty]
    private int _availableCount;

    [ObservableProperty]
    private int _favoriteCount;

    [ObservableProperty]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasSelection;

    [ObservableProperty]
    private bool _isBatchInstalling;

    [ObservableProperty]
    private string _batchProgressText = string.Empty;

    public event Action? RequestOpenSettings;
    public event Action? RequestOpenHistory;
    public event Action? RequestOpenAbout;

    public MainViewModel(
        ILauncherOrchestratorService orchestratorService,
        ISettingsService settingsService,
        ILauncherFavoritesService favoritesService,
        ILauncherArtworkService artworkService)
    {
        _orchestratorService = orchestratorService;
        _settingsService = settingsService;
        _favoritesService = favoritesService;
        _artworkService = artworkService;

        _orchestratorService.PlatformStateChanged += OnPlatformStateChanged;
        _favoritesService.FavoritesChanged += OnFavoritesChanged;
    }

    public async Task InitializeAsync()
    {
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            ViewMode = string.IsNullOrWhiteSpace(settings.ViewMode) ? "Cards" : settings.ViewMode;
        }
        catch
        {
            ViewMode = "Cards";
        }

        await LoadPlatformsAsync();
    }

    [RelayCommand]
    public async Task SetViewModeAsync(string mode)
    {
        if (ViewMode == mode) return;

        ViewMode = mode;
        try
        {
            var settings = await _settingsService.LoadSettingsAsync();
            settings.ViewMode = mode;
            await _settingsService.SaveSettingsAsync(settings);
        }
        catch
        {
            // Tratar silenciosamente erro de persistência rápida
        }
    }

    [RelayCommand]
    public async Task LoadPlatformsAsync()
    {
        IsLoading = true;
        StatusMessage = "Sincronizando plataformas e verificando o sistema operacional...";

        try
        {
            var platforms = await _orchestratorService.LoadAndDetectAllAsync();

            _allCards.Clear();
            foreach (var platform in platforms)
            {
                var cardVm = new LauncherCardViewModel(platform, _orchestratorService, _favoritesService);
                cardVm.SyncFromModel();
                cardVm.ArtworkPath = _artworkService.GetArtworkPath(platform.Id);
                cardVm.SelectionChanged += OnCardSelectionChanged;
                _allCards.Add(cardVm);
            }

            ApplyFilter();
            UpdateMetrics();
            UpdateSelectionMetrics();
            StatusMessage = "Catálogo atualizado";

            // Disparar busca de artes SteamGridDB em segundo plano sem bloquear a interface
            _ = FetchArtworksInBackgroundAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Falha ao carregar catálogo: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task FetchArtworksInBackgroundAsync()
    {
        foreach (var card in _allCards.ToList())
        {
            if (string.IsNullOrEmpty(card.ArtworkPath))
            {
                try
                {
                    var artPath = await _artworkService.GetOrFetchArtworkAsync(card.Model);
                    if (!string.IsNullOrEmpty(artPath))
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            card.ArtworkPath = artPath;
                        });
                    }
                }
                catch
                {
                    // Fallback transparente para o ícone local
                }
            }
        }
    }

    [RelayCommand]
    public async Task RefreshArtworkAsync()
    {
        StatusMessage = "Atualizando imagens dos launchers via SteamGridDB...";
        try
        {
            await _artworkService.RefreshAllArtworksAsync(_allCards.Select(c => c.Model));
            foreach (var card in _allCards)
            {
                card.ArtworkPath = _artworkService.GetArtworkPath(card.Id);
            }
            StatusMessage = "Imagens dos launchers atualizadas com sucesso.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao atualizar imagens: {ex.Message}";
        }
    }

    private void OnCardSelectionChanged(LauncherCardViewModel card)
    {
        UpdateSelectionMetrics();
    }

    private void OnFavoritesChanged()
    {
        UpdateMetrics();
        if (SelectedFilter == "Favoritos")
        {
            ApplyFilter();
        }
    }

    private void UpdateSelectionMetrics()
    {
        SelectedCount = _allCards.Count(c => c.IsSelected && c.CanBeSelected);
        HasSelection = SelectedCount > 0;
    }

    [RelayCommand]
    private void SelectAllAvailable()
    {
        foreach (var card in _allCards.Where(c => c.CanBeSelected))
        {
            card.IsSelected = true;
        }
        UpdateSelectionMetrics();
    }

    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var card in _allCards)
        {
            card.IsSelected = false;
        }
        UpdateSelectionMetrics();
    }

    [RelayCommand]
    private async Task InstallSelectedAsync()
    {
        var selectedCards = _allCards.Where(c => c.IsSelected && c.CanBeSelected).ToList();
        if (selectedCards.Count == 0 || IsBatchInstalling)
            return;

        IsBatchInstalling = true;
        int total = selectedCards.Count;
        int successCount = 0;

        try
        {
            for (int i = 0; i < total; i++)
            {
                var card = selectedCards[i];
                BatchProgressText = $"Instalando {card.Name} ({i + 1}/{total})...";
                StatusMessage = BatchProgressText;

                try
                {
                    await _orchestratorService.DownloadAndInstallLauncherAsync(card.Model);
                    card.SyncFromModel();
                    card.IsSelected = false;
                    if (card.State == LauncherState.INSTALLED)
                    {
                        successCount++;
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"Erro ao instalar {card.Name}: {ex.Message}";
                }
            }
        }
        finally
        {
            IsBatchInstalling = false;
            BatchProgressText = string.Empty;
            UpdateSelectionMetrics();
            UpdateMetrics();
            StatusMessage = $"Instalação em lote concluída: {successCount}/{total} plataformas instaladas com sucesso.";
        }
    }

    [RelayCommand]
    private void SetFilter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        RequestOpenSettings?.Invoke();
    }

    [RelayCommand]
    private void OpenHistory()
    {
        RequestOpenHistory?.Invoke();
    }

    [RelayCommand]
    private void OpenAbout()
    {
        RequestOpenAbout?.Invoke();
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredCards.Clear();

        var query = _allCards.AsEnumerable();

        // 1. Filtro por Categoria / Estado
        if (SelectedFilter == "Favoritos")
        {
            query = query.Where(c => c.IsFavorite);
        }
        else if (SelectedFilter == "Instalados")
        {
            query = query.Where(c => c.State == LauncherState.INSTALLED || c.State == LauncherState.RUNNING);
        }
        else if (SelectedFilter == "Disponíveis")
        {
            query = query.Where(c => c.State == LauncherState.NOT_INSTALLED);
        }
        else if (SelectedFilter == "Incompatíveis")
        {
            query = query.Where(c => c.State == LauncherState.UNSUPPORTED);
        }

        // 2. Filtro por Busca Textual
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            query = query.Where(c =>
                c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.Tagline.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var card in query)
        {
            FilteredCards.Add(card);
        }
    }

    private void UpdateMetrics()
    {
        TotalCount = _allCards.Count;
        InstalledCount = _allCards.Count(c => c.State == LauncherState.INSTALLED || c.State == LauncherState.RUNNING);
        AvailableCount = _allCards.Count(c => c.State == LauncherState.NOT_INSTALLED);
        FavoriteCount = _allCards.Count(c => c.IsFavorite);
    }

    private void OnPlatformStateChanged(object? sender, LauncherPlatform platform)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            var card = _allCards.FirstOrDefault(c => c.Id == platform.Id);
            if (card != null)
            {
                card.SyncFromModel();
                UpdateMetrics();
            }
        });
    }
}
