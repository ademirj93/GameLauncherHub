using System.Windows;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.UI.ViewModels;
using GameLauncherHub.UI.Views.Dialogs;
using Microsoft.Extensions.DependencyInjection;

namespace GameLauncherHub.UI;

/// <summary>
/// Lógica de interação da janela principal do GameLauncher Hub.
/// Gerencia fechamento/minimização para o System Tray e acionamento de diálogos.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ISettingsService _settingsService;
    private readonly ISystemTrayService _systemTrayService;
    private bool _isExplicitExit;

    public MainWindow(
        MainViewModel viewModel,
        IServiceProvider serviceProvider,
        ISettingsService settingsService,
        ISystemTrayService systemTrayService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _serviceProvider = serviceProvider;
        _settingsService = settingsService;
        _systemTrayService = systemTrayService;
        DataContext = _viewModel;

        _viewModel.RequestOpenSettings += OnOpenSettings;
        _viewModel.RequestOpenHistory += OnOpenHistory;
        _viewModel.RequestOpenAbout += OnOpenAbout;

        Loaded += async (s, e) =>
        {
            await _viewModel.InitializeAsync();
        };

        // Integração com eventos da bandeja do sistema (System Tray)
        _systemTrayService.RequestShowWindow += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                Activate();
                Focus();
            });
        };

        _systemTrayService.RequestOpenSettings += () =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                Activate();
                OnOpenSettings();
            });
        };

        _systemTrayService.RequestShutdown += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _isExplicitExit = true;
                Close();
                System.Windows.Application.Current.Shutdown();
            });
        };

        // Comportamento de minimização
        StateChanged += (s, e) =>
        {
            if (WindowState == WindowState.Minimized)
            {
                try
                {
                    var settings = _settingsService.CurrentSettings;
                    if (settings.EnableSystemTray && settings.MinimizeToTray)
                    {
                        Hide();
                    }
                }
                catch
                {
                    // Fallback para minimização normal do Windows
                }
            }
        };

        // Interceptação de fechamento da janela (Close to Tray)
        Closing += (s, e) =>
        {
            if (_isExplicitExit)
            {
                return;
            }

            try
            {
                var settings = _settingsService.CurrentSettings;
                if (settings.EnableSystemTray && settings.CloseToTray)
                {
                    e.Cancel = true;
                    Hide();
                    return;
                }
            }
            catch
            {
            }

            _isExplicitExit = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                System.Windows.Application.Current.Shutdown();
            }));
        };
    }

    private void OnOpenSettings()
    {
        var settingsVm = _serviceProvider.GetRequiredService<SettingsViewModel>();
        var dialog = new SettingsDialog(settingsVm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnOpenHistory()
    {
        var historyVm = _serviceProvider.GetRequiredService<HistoryViewModel>();
        var dialog = new HistoryDialog(historyVm)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void OnOpenAbout()
    {
        var dialog = new AboutDialog
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
