using System.Reflection;
using System.Windows;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Diálogo "Sobre o GameLauncher Hub" contendo informações de release, desenvolvedor, licença GNU
/// e verificação de novas atualizações oficiais no GitHub.
/// </summary>
public partial class AboutDialog : Window
{
    private readonly IAppUpdateService? _updateService;
    private AppUpdateInfo? _latestUpdateInfo;

    public event Action<AppUpdateInfo>? RequestOpenUpdateDialog;

    public AboutDialog(IAppUpdateService? updateService = null)
    {
        InitializeComponent();
        _updateService = updateService;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(infoVer))
        {
            var cleanVer = infoVer.Split('+')[0].Trim();
            VersionTextBlock.Text = $"Release {cleanVer}";
            return;
        }

        if (_updateService != null)
        {
            var version = _updateService.GetCurrentVersion();
            var revSuffix = version.Revision > 0 ? $"b{version.Revision}" : string.Empty;
            VersionTextBlock.Text = $"Release {version.Major}.{version.Minor}.{version.Build}{revSuffix}";
        }
    }

    private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_updateService == null) return;

        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Consultando...";
        UpdateStatusTextBlock.Text = "Conectando ao GitHub Releases para verificar novidades...";
        OpenUpdateButton.Visibility = Visibility.Collapsed;

        try
        {
            var updateInfo = await _updateService.CheckForUpdatesAsync();
            _latestUpdateInfo = updateInfo;

            if (updateInfo.IsUpdateAvailable)
            {
                UpdateStatusTextBlock.Text = $"Nova versão {updateInfo.TagName} encontrada!";
                OpenUpdateButton.Visibility = Visibility.Visible;
            }
            else if (!string.IsNullOrEmpty(updateInfo.ErrorMessage))
            {
                UpdateStatusTextBlock.Text = updateInfo.ErrorMessage;
            }
            else
            {
                UpdateStatusTextBlock.Text = "Você já está executando a versão mais recente.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusTextBlock.Text = $"Falha ao verificar: {ex.Message}";
        }
        finally
        {
            CheckUpdatesButton.IsEnabled = true;
            CheckUpdatesButton.Content = "Verificar";
        }
    }

    private void OpenUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (_latestUpdateInfo != null)
        {
            RequestOpenUpdateDialog?.Invoke(_latestUpdateInfo);
            Close();
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
