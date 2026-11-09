using System.Windows;
using GameLauncherHub.UI.ViewModels;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Interaction logic for SettingsDialog.xaml
/// </summary>
public partial class SettingsDialog : Window
{
    public SettingsDialog(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
            DialogResult = true;
            Close();
        };

        Loaded += async (s, e) =>
        {
            await viewModel.InitializeAsync();
        };
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

