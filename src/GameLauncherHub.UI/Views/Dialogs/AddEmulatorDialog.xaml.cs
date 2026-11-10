using System.IO;
using System.Windows;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Models;
using Microsoft.Win32;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Diálogo modal para cadastro e integração de emuladores personalizados ao Hub.
/// </summary>
public partial class AddEmulatorDialog : Window
{
    private readonly ILauncherOrchestratorService _orchestratorService;

    public LauncherPlatform? CreatedPlatform { get; private set; }

    public AddEmulatorDialog(ILauncherOrchestratorService orchestratorService)
    {
        InitializeComponent();
        _orchestratorService = orchestratorService;
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Selecionar Executável do Emulador",
            Filter = "Executáveis (*.exe)|*.exe|Todos os arquivos (*.*)|*.*",
            CheckFileExists = true
        };

        if (openFileDialog.ShowDialog(this) == true)
        {
            ExecutablePathTextBox.Text = openFileDialog.FileName;

            // Se o campo de nome ainda estiver em branco, sugere o nome do arquivo sem extensão
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
                NameTextBox.Text = fileNameWithoutExt;
            }
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        ErrorMessageTextBlock.Visibility = Visibility.Collapsed;

        var name = NameTextBox.Text?.Trim();
        var system = SystemComboBox.Text?.Trim();
        var exePath = ExecutablePathTextBox.Text?.Trim();
        var args = ArgumentsTextBox.Text?.Trim();
        var website = WebsiteTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Por favor, informe o nome do emulador.");
            NameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(exePath))
        {
            ShowError("Por favor, selecione o arquivo executável (.exe) do emulador.");
            ExecutablePathTextBox.Focus();
            return;
        }

        if (!File.Exists(exePath))
        {
            ShowError($"O arquivo executável não foi encontrado no caminho especificado:\n{exePath}");
            ExecutablePathTextBox.Focus();
            return;
        }

        var id = $"custom_emu_{Guid.NewGuid().ToString("N")[..8]}";
        var fileName = Path.GetFileName(exePath);

        var platform = new LauncherPlatform
        {
            Id = id,
            Name = name,
            Category = "Emulator",
            TargetSystem = !string.IsNullOrWhiteSpace(system) ? system : "Emulador Customizado",
            Tagline = !string.IsNullOrWhiteSpace(system) ? $"Emulador de {system}" : "Emulador Personalizado",
            Description = $"Emulador cadastrado manualmente pelo usuário. Executável: {fileName}",
            BrandColor = "#1C202C",
            AccentColor = "#6366F1",
            IconKey = "IconGamepad",
            OfficialWebsite = !string.IsNullOrWhiteSpace(website) ? website : "https://github.com",
            DownloadPageUrl = !string.IsNullOrWhiteSpace(website) ? website : "https://github.com",
            ExpectedExecutableName = fileName,
            IsOpenSource = true,
            IsCustom = true,
            DetectedExecutablePath = exePath,
            State = LauncherState.INSTALLED,
            StatusMessage = "Configurado manualmente",
            Detection = new DetectionRules
            {
                KnownExecutableNames = new List<string> { fileName },
                DefaultInstallDirectories = new List<string> { Path.GetDirectoryName(exePath) ?? string.Empty }
            },
            Execution = new ExecutionRules
            {
                DefaultArguments = args
            }
        };

        try
        {
            await _orchestratorService.AddCustomPlatformAsync(platform);
            CreatedPlatform = platform;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Falha ao cadastrar o emulador: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorMessageTextBlock.Text = message;
        ErrorMessageTextBlock.Visibility = Visibility.Visible;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
