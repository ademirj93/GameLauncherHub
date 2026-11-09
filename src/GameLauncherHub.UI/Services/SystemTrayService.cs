using System.Drawing;
using System.IO;
using System.Windows.Forms;
using GameLauncherHub.Application.Interfaces;
using GameLauncherHub.Domain.Enums;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.UI.Services;

/// <summary>
/// Implementação profissional do serviço de bandeja do sistema (System Tray) para Windows.
/// Utiliza System.Windows.Forms.NotifyIcon nativo para estabilidade total no Windows.
/// </summary>
public sealed class SystemTrayService : ISystemTrayService
{
    private readonly ISettingsService _settingsService;
    private readonly ILauncherOrchestratorService _orchestratorService;
    private readonly ILauncherFavoritesService _favoritesService;
    private readonly ILogger<SystemTrayService> _logger;

    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;
    private bool _isDisposed;
    private IReadOnlyList<LauncherPlatform> _cachedPlatforms = [];

    public event Action? RequestShowWindow;
    public event Action? RequestOpenSettings;
    public event Action? RequestShutdown;

    public SystemTrayService(
        ISettingsService settingsService,
        ILauncherOrchestratorService orchestratorService,
        ILauncherFavoritesService favoritesService,
        ILogger<SystemTrayService> logger)
    {
        _settingsService = settingsService;
        _orchestratorService = orchestratorService;
        _favoritesService = favoritesService;
        _logger = logger;
    }

    public void Initialize()
    {
        if (_notifyIcon != null) return;

        try
        {
            var icon = LoadApplicationIcon();

            _contextMenu = new ContextMenuStrip();
            _notifyIcon = new NotifyIcon
            {
                Text = "GameLauncher Hub",
                Icon = icon,
                Visible = true,
                ContextMenuStrip = _contextMenu
            };

            _notifyIcon.DoubleClick += (s, e) =>
            {
                ExecuteOnUi(() => RequestShowWindow?.Invoke());
            };

            // Escutar alterações nos launchers, favoritos e configurações para manter menu atualizado
            _orchestratorService.PlatformStateChanged += OnPlatformStateChanged;
            _favoritesService.FavoritesChanged += OnFavoritesChanged;
            _settingsService.SettingsChanged += OnSettingsChanged;

            // Carregar estado inicial
            _ = Task.Run(async () =>
            {
                try
                {
                    var platforms = await _orchestratorService.LoadAndDetectAllAsync();
                    _cachedPlatforms = platforms;
                    RefreshMenu();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao carregar plataformas para o System Tray.");
                }
            });

            _logger.LogInformation("SystemTrayService inicializado com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao inicializar SystemTrayService.");
        }
    }

    private void OnPlatformStateChanged(object? sender, LauncherPlatform platform)
    {
        ExecuteOnUi(RefreshMenu);
    }

    private void OnFavoritesChanged()
    {
        ExecuteOnUi(RefreshMenu);
    }

    private void OnSettingsChanged(AppSettings settings)
    {
        ExecuteOnUi(RefreshMenu);
    }

    public void SetVisible(bool visible)
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = visible;
        }
    }

    public void ShowBalloonNotification(string title, string message, int timeoutMs = 3000)
    {
        if (_notifyIcon != null && _notifyIcon.Visible)
        {
            _notifyIcon.ShowBalloonTip(timeoutMs, title, message, ToolTipIcon.Info);
        }
    }

    public void RefreshMenu()
    {
        if (_isDisposed || _notifyIcon == null || _contextMenu == null) return;

        ExecuteOnUi(() =>
        {
            try
            {
                _contextMenu.Items.Clear();

                // Cabeçalho da aplicação
                var headerItem = new ToolStripMenuItem("🎮 GameLauncher Hub")
                {
                    Enabled = false,
                    Font = new Font(_contextMenu.Font, System.Drawing.FontStyle.Bold)
                };
                _contextMenu.Items.Add(headerItem);
                _contextMenu.Items.Add(new ToolStripSeparator());

                // Obter preferências de exibição do Tray (direto de CurrentSettings síncrono e thread-safe)
                var settings = _settingsService.CurrentSettings;
                bool favoritesOnly = string.Equals(settings.TrayDisplayMode, "FavoritesOnly", StringComparison.OrdinalIgnoreCase);

                // Filtrar launchers instalados
                var availableLaunchers = _cachedPlatforms
                    .Where(p => p.State == LauncherState.INSTALLED || p.State == LauncherState.RUNNING)
                    .Where(p => !favoritesOnly || _favoritesService.IsFavorite(p.Id))
                    .OrderBy(p => p.Name)
                    .ToList();

                if (availableLaunchers.Count > 0)
                {
                    foreach (var platform in availableLaunchers)
                    {
                        var isFav = _favoritesService.IsFavorite(platform.Id);
                        var itemLabel = isFav ? $"★ {platform.Name}" : platform.Name;
                        var menuItem = new ToolStripMenuItem(itemLabel);

                        var capturedPlatform = platform;
                        menuItem.Click += async (s, e) =>
                        {
                            try
                            {
                                await _orchestratorService.ExecuteLauncherAsync(capturedPlatform);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Erro ao executar launcher '{Name}' via System Tray.", capturedPlatform.Name);
                            }
                        };

                        _contextMenu.Items.Add(menuItem);
                    }
                }
                else
                {
                    var emptyText = favoritesOnly
                        ? "(Nenhum favorito instalado)"
                        : "(Nenhum launcher instalado)";
                    var emptyItem = new ToolStripMenuItem(emptyText) { Enabled = false };
                    _contextMenu.Items.Add(emptyItem);
                }

                _contextMenu.Items.Add(new ToolStripSeparator());

                // Ação: Abrir Janela Principal
                var openItem = new ToolStripMenuItem("Abrir GameLauncher Hub");
                openItem.Click += (s, e) => RequestShowWindow?.Invoke();
                _contextMenu.Items.Add(openItem);

                // Ação: Configurações
                var settingsItem = new ToolStripMenuItem("Configurações...");
                settingsItem.Click += (s, e) => RequestOpenSettings?.Invoke();
                _contextMenu.Items.Add(settingsItem);

                _contextMenu.Items.Add(new ToolStripSeparator());

                // Ação: Sair
                var exitItem = new ToolStripMenuItem("Sair");
                exitItem.Click += (s, e) => RequestShutdown?.Invoke();
                _contextMenu.Items.Add(exitItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar menu de contexto do System Tray.");
            }
        });
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/GameLauncherHub.UI;component/Resources/icon.ico");
            var streamInfo = System.Windows.Application.GetResourceStream(uri);
            if (streamInfo?.Stream != null)
            {
                return new Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // Fallback caso pack URI falhe
        }

        // Tentar extrair do executável ou arquivo local
        var localIconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
        if (File.Exists(localIconPath))
        {
            return new Icon(localIconPath);
        }

        return SystemIcons.Application;
    }

    private static void ExecuteOnUi(Action action)
    {
        var app = System.Windows.Application.Current;
        if (app == null)
        {
            action();
            return;
        }

        if (app.Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            app.Dispatcher.Invoke(action);
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _orchestratorService.PlatformStateChanged -= OnPlatformStateChanged;
        _favoritesService.FavoritesChanged -= OnFavoritesChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;

        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip = null;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }

        if (_contextMenu != null)
        {
            _contextMenu.Dispose();
            _contextMenu = null;
        }

        GC.SuppressFinalize(this);
    }
}

