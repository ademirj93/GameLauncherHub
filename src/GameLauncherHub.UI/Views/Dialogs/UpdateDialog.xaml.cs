using System.Windows;
using GameLauncherHub.UI.ViewModels;

namespace GameLauncherHub.UI.Views.Dialogs;

/// <summary>
/// Janela modal para exibição de nova versão disponível, changelog e download com instalação automática.
/// </summary>
public partial class UpdateDialog : Window
{
    private readonly UpdateViewModel _viewModel;

    public UpdateDialog(UpdateViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestClose += () =>
        {
            Dispatcher.Invoke(Close);
        };
    }
}

