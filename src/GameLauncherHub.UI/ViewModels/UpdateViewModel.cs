using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.UI.ViewModels;

/// <summary>
/// ViewModel da janela de atualização do GameLauncher Hub.
/// Gerencia apresentação de notas de release, download com acompanhamento de progresso
/// e inicialização do instalador baixado.
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private readonly IAppUpdateService _updateService;
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private AppUpdateInfo _updateInfo = new();

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private double _downloadPercentage;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isDownloadComplete;

    [ObservableProperty]
    private string? _downloadedInstallerPath;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    public event Action? RequestClose;

    public UpdateViewModel(IAppUpdateService updateService)
    {
        _updateService = updateService;
    }

    public void Initialize(AppUpdateInfo info)
    {
        UpdateInfo = info;
        IsDownloading = false;
        IsDownloadComplete = false;
        DownloadPercentage = 0;
        DownloadedInstallerPath = null;
        ErrorMessage = null;
        HasError = false;
        StatusMessage = "Nova versão pronta para download.";
    }

    [RelayCommand]
    public async Task DownloadAndInstallAsync()
    {
        if (IsDownloading) return;

        if (string.IsNullOrWhiteSpace(UpdateInfo.InstallerDownloadUrl))
        {
            // Se a release não tiver um .exe anexado, direciona para o navegador
            OpenGitHubRelease();
            return;
        }

        IsDownloading = true;
        HasError = false;
        ErrorMessage = null;
        StatusMessage = "Conectando ao GitHub para download do instalador...";
        DownloadPercentage = 0;
        _downloadCts = new CancellationTokenSource();

        var progress = new Progress<DownloadProgressInfo>(info =>
        {
            DownloadPercentage = info.Percentage;
            DownloadSpeedText = info.TransferSpeedText;
            StatusMessage = info.StatusText;

            if (info.TotalBytesToReceive.HasValue && info.TotalBytesToReceive > 0)
            {
                const double mb = 1024 * 1024;
                var currentMb = info.BytesReceived / mb;
                var totalMb = info.TotalBytesToReceive.Value / mb;
                DownloadProgressText = $"{currentMb:F1} MB de {totalMb:F1} MB ({info.Percentage:F0}%)";
            }
            else
            {
                const double mb = 1024 * 1024;
                DownloadProgressText = $"{info.BytesReceived / mb:F1} MB baixados";
            }
        });

        try
        {
            DownloadedInstallerPath = await _updateService.DownloadUpdateAsync(
                UpdateInfo,
                progress,
                _downloadCts.Token);

            IsDownloadComplete = true;
            IsDownloading = false;
            StatusMessage = "Download concluído com sucesso! Iniciando instalador...";

            // Dispara a execução do instalador e solicita o encerramento do app
            _updateService.LaunchInstallerAndExit(DownloadedInstallerPath, silent: false);
        }
        catch (OperationCanceledException)
        {
            IsDownloading = false;
            StatusMessage = "Download cancelado pelo usuário.";
            DownloadPercentage = 0;
            DownloadProgressText = string.Empty;
        }
        catch (Exception ex)
        {
            IsDownloading = false;
            HasError = true;
            ErrorMessage = $"Falha ao baixar atualização: {ex.Message}";
            StatusMessage = "Erro durante o download da atualização.";
        }
        finally
        {
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    [RelayCommand]
    public void CancelDownload()
    {
        if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
        {
            _downloadCts.Cancel();
        }
    }

    [RelayCommand]
    public void OpenGitHubRelease()
    {
        var url = !string.IsNullOrWhiteSpace(UpdateInfo.HtmlUrl)
            ? UpdateInfo.HtmlUrl
            : "https://github.com/ademirj93/GameLauncherHub/releases";

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Não foi possível abrir o navegador: {ex.Message}";
        }
    }

    [RelayCommand]
    public void Dismiss()
    {
        CancelDownload();
        RequestClose?.Invoke();
    }
}

