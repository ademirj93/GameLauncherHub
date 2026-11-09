using System.Windows;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Diálogo "Sobre o GameLauncher Hub" contendo informações de release, desenvolvedor e licença GNU.
/// </summary>
public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

