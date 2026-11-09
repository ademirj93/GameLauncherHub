using System.Windows;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Application.Services;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Infrastructure.Artwork;
using GameLauncherHub.Infrastructure.Catalog;
using GameLauncherHub.Infrastructure.Detection;
using GameLauncherHub.Infrastructure.Downloads;
using GameLauncherHub.Infrastructure.Favorites;
using GameLauncherHub.Infrastructure.Security;
using GameLauncherHub.Infrastructure.Services;
using GameLauncherHub.Infrastructure.SteamGridDB;
using GameLauncherHub.UI.Services;
using GameLauncherHub.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.UI;

/// <summary>
/// Ponto de entrada e composição do container de Injeção de Dependências da aplicação.
/// </summary>
public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        ConfigureUnhandledExceptionLogging();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);

        _serviceProvider = serviceCollection.BuildServiceProvider();

        // 1. Inicializa o serviço do System Tray (Bandeja do Windows)
        var systemTrayService = _serviceProvider.GetRequiredService<ISystemTrayService>();
        systemTrayService.Initialize();

        // 2. Apresenta a janela principal
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // 1. Logging Estruturado
        services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        // 2. Infraestrutura & Catálogo Oficial
        services.AddSingleton<IOfficialDownloadCatalogService, OfficialDownloadCatalogService>();
        services.AddSingleton<ICatalogService, CatalogService>();

        // 3. Estratégias de Detecção de Instalação no Windows
        services.AddSingleton<IDetectionStrategy, RegistryUninstallDetectionStrategy>();
        services.AddSingleton<IDetectionStrategy, AppPathsDetectionStrategy>();
        services.AddSingleton<IDetectionStrategy, ProtocolRegistrationDetectionStrategy>();
        services.AddSingleton<IDetectionStrategy, KnownPathsDetectionStrategy>();
        services.AddSingleton<IDetectionStrategy, StartMenuDetectionStrategy>();
        services.AddSingleton<ILauncherDetectionService, LauncherDetectionService>();
        services.AddSingleton<ILauncherExecutionService, LauncherExecutionService>();

        // 4. Segurança, Downloads e Verificação Authenticode
        services.AddSingleton<AuthenticodeVerifier>();
        services.AddSingleton<IInstallerSecurityService, InstallerSecurityService>();
        services.AddSingleton<ILauncherDownloadResolver, LauncherDownloadResolver>();
        services.AddSingleton<IDownloadManager, HttpDownloadManager>();
        services.AddSingleton<IInstallerService, InstallerService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IOperationHistoryService, OperationHistoryService>();

        // 5. SteamGridDB & Sistema de Artes
        services.AddSingleton<ISteamGridDBService, SteamGridDBService>();
        services.AddSingleton<IArtworkCacheService, ArtworkCacheService>();
        services.AddSingleton<ILauncherArtworkService, LauncherArtworkService>();

        // 6. Sistema de Favoritos e System Tray
        services.AddSingleton<ILauncherFavoritesService, LauncherFavoritesService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();

        // 7. Aplicação & Casos de Uso
        services.AddSingleton<ILauncherOrchestratorService, LauncherOrchestratorService>();

        // 8. ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HistoryViewModel>();

        // 9. Views
        services.AddTransient<MainWindow>();
    }

    private void ConfigureUnhandledExceptionLogging()
    {
        // Tratamento global de exceções da UI thread
        DispatcherUnhandledException += (s, args) =>
        {
            System.Windows.MessageBox.Show(
                $"Ocorreu um erro inesperado na aplicação:\n\n{args.Exception.Message}",
                "GameLauncher Hub - Erro",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            args.Handled = true;
        };

        // Tratamento global de exceções em background tasks
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            args.SetObserved();
        };

        // Tratamento global de exceções no AppDomain
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Erro crítico não tratado no processo:\n\n{ex.Message}",
                    "GameLauncher Hub - Erro Crítico",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            var systemTray = _serviceProvider?.GetService<ISystemTrayService>();
            systemTray?.Dispose();
        }
        catch
        {
        }

        try
        {
            _serviceProvider?.Dispose();
        }
        catch
        {
        }

        base.OnExit(e);
        Environment.Exit(0);
    }
}
