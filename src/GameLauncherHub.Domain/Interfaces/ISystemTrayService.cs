namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do serviço do System Tray (Bandeja do Sistema) do Windows.
/// Responsável pelo ciclo de vida do ícone, menus de contexto dinâmicos e eventos de interação.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    event Action? RequestShowWindow;
    event Action? RequestOpenSettings;
    event Action? RequestShutdown;

    void Initialize();
    void RefreshMenu();
    void SetVisible(bool visible);
    void ShowBalloonNotification(string title, string message, int timeoutMs = 3000);
}

