using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.UI.ViewModels;

/// <summary>
/// ViewModel reativo para o Card e Linha Compacta de uma plataforma individual.
/// </summary>
public partial class LauncherCardViewModel : ObservableObject
{
    private readonly ILauncherOrchestratorService _orchestratorService;
    private readonly ILauncherFavoritesService _favoritesService;

    public LauncherPlatform Model { get; }

    [ObservableProperty]
    private LauncherState _state;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _detectedVersion;

    [ObservableProperty]
    private double _operationProgress;

    [ObservableProperty]
    private string? _operationSpeed;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private string? _artworkPath;

    public event Action<LauncherCardViewModel>? SelectionChanged;

    public bool CanBeSelected => (State == LauncherState.NOT_INSTALLED || State == LauncherState.ERROR) && IsSupportedOnWindows;

    public string Id => Model.Id;
    public string Name => Model.Name;
    public string Tagline => Model.Tagline;
    public string Description => Model.Description;
    public string BrandColor => Model.BrandColor;
    public string AccentColor => Model.AccentColor;
    public string SecondaryColor => Model.SecondaryColor;
    public string IconKey => Model.IconKey;
    public string OfficialWebsite => Model.OfficialWebsite;
    public bool IsSupportedOnWindows => Model.IsSupportedOnWindows;
    public string? UnsupportedReason => Model.UnsupportedReason;

    public LauncherCardViewModel(
        LauncherPlatform model,
        ILauncherOrchestratorService orchestratorService,
        ILauncherFavoritesService favoritesService)
    {
        Model = model;
        _orchestratorService = orchestratorService;
        _favoritesService = favoritesService;

        _state = model.State;
        _statusMessage = model.StatusMessage;
        _detectedVersion = model.DetectedVersion;
        _operationProgress = model.OperationProgress;
        _operationSpeed = model.OperationSpeed;
        _isFavorite = favoritesService.IsFavorite(model.Id);

        _favoritesService.FavoritesChanged += OnFavoritesChanged;
    }

    private void OnFavoritesChanged()
    {
        IsFavorite = _favoritesService.IsFavorite(Model.Id);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        SelectionChanged?.Invoke(this);
    }

    public void SyncFromModel()
    {
        State = Model.State;
        StatusMessage = Model.StatusMessage;
        DetectedVersion = Model.DetectedVersion;
        OperationProgress = Model.OperationProgress;
        OperationSpeed = Model.OperationSpeed;
        IsBusy = Model.State == LauncherState.DOWNLOADING || Model.State == LauncherState.INSTALLING;
        IsFavorite = _favoritesService.IsFavorite(Model.Id);
        OnPropertyChanged(nameof(CanBeSelected));
        if (!CanBeSelected)
        {
            IsSelected = false;
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        IsFavorite = await _favoritesService.ToggleFavoriteAsync(Model.Id);
    }

    public IRelayCommand ActionCommand => ExecuteActionCommand;

    [RelayCommand]
    private async Task ExecuteActionAsync()
    {
        if (IsBusy) return;

        // 1. Plataforma não suportada
        if (!IsSupportedOnWindows)
        {
            OpenWebsite();
            return;
        }

        // 2. Plataforma instalada ou em execução -> Inicia o aplicativo
        if (State == LauncherState.INSTALLED || State == LauncherState.RUNNING)
        {
            IsBusy = true;
            try
            {
                await _orchestratorService.ExecuteLauncherAsync(Model);
                SyncFromModel();
            }
            finally
            {
                IsBusy = false;
            }
            return;
        }

        // 3. Caso especial: Aplicativo Microsoft Store / Xbox não instalado
        if (Model.IsWindowsStorePackage && State == LauncherState.NOT_INSTALLED)
        {
            try
            {
                var storeUrl = "ms-windows-store://pdp/?productid=9MV0B5HZVK9Z";
                var psi = new ProcessStartInfo
                {
                    FileName = storeUrl,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch
            {
                OpenWebsite();
            }
            return;
        }

        // 4. Download oficial, validação Authenticode e instalação
        if (State == LauncherState.NOT_INSTALLED || State == LauncherState.ERROR)
        {
            IsBusy = true;
            try
            {
                await _orchestratorService.DownloadAndInstallLauncherAsync(Model);
                SyncFromModel();
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    [RelayCommand]
    private void OpenWebsite()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = OfficialWebsite,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch
        {
            // Tratar silenciosamente se navegador padrão falhar
        }
    }

    [RelayCommand]
    private async Task RefreshStatusAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            await _orchestratorService.RefreshPlatformStatusAsync(Model);
            SyncFromModel();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
