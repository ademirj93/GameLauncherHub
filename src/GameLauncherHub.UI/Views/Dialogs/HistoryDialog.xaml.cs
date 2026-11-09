using System.Windows;
using GameLauncherHub.UI.ViewModels;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Interaction logic for HistoryDialog.xaml
/// </summary>
public partial class HistoryDialog : Window
{
    public HistoryDialog(HistoryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestClose += () =>
        {
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

