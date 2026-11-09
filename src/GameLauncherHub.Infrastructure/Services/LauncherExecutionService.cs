using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Serviço de execução de launchers no Windows.
/// Gerencia inicialização por executável verificado ou protocolo registrado,
/// previne instâncias duplicadas, restaura janelas ativas e limpa instâncias órfãs em segundo plano.
/// </summary>
public class LauncherExecutionService : ILauncherExecutionService
{
    private readonly ILogger<LauncherExecutionService> _logger;
    private readonly ILauncherDetectionService _detectionService;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    public LauncherExecutionService(ILogger<LauncherExecutionService> logger, ILauncherDetectionService detectionService)
    {
        _logger = logger;
        _detectionService = detectionService;
    }

    public async Task<ExecutionResult> LaunchAsync(LauncherPlatform platform, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!platform.IsSupportedOnWindows)
            {
                return new ExecutionResult(false, null, platform.UnsupportedReason ?? "Plataforma não suportada no Windows.");
            }

            // 1. Verifica se o processo já está ativo no sistema operacional
            bool isAlreadyRunning = await _detectionService.IsRunningAsync(platform, cancellationToken);
            if (isAlreadyRunning)
            {
                _logger.LogInformation("Launcher {Name} detectado em execução no sistema. Verificando janela ativa...", platform.Name);

                // Procura processo com janela gráfica ativa para restaurar
                var matchingProcesses = GetMatchingProcesses(platform);
                var activeWindowProcess = matchingProcesses.FirstOrDefault(p =>
                {
                    try { return p.MainWindowHandle != IntPtr.Zero; } catch { return false; }
                });

                if (activeWindowProcess != null)
                {
                    try
                    {
                        ShowWindowAsync(activeWindowProcess.MainWindowHandle, SW_RESTORE);
                        SetForegroundWindow(activeWindowProcess.MainWindowHandle);
                        return new ExecutionResult(true, activeWindowProcess.Id, "Janela do launcher ativada e trazida para o primeiro plano.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Falha ao ativar janela do processo PID {Pid}", activeWindowProcess.Id);
                    }
                }
                else if (matchingProcesses.Count > 0)
                {
                    // Processo rodando em segundo plano sem janela gráfica (instância órfã/zombie, comum em Tauri/WebView2)
                    _logger.LogWarning("Processo de {Name} detectado sem interface ativa. Encerrando instância órfã para liberar recursos...", platform.Name);
                    foreach (var proc in matchingProcesses)
                    {
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(1500);
                        }
                        catch (Exception exKill)
                        {
                            _logger.LogDebug(exKill, "Falha ao finalizar processo órfão PID {Pid}", proc.Id);
                        }
                    }
                    isAlreadyRunning = false;
                }
            }

            // 2. Execução de aplicativos da Windows Store / MSIX (ex: Xbox App)
            if (platform.IsWindowsStorePackage)
            {
                var storeUri = !string.IsNullOrWhiteSpace(platform.Execution?.ProtocolScheme)
                    ? platform.Execution.ProtocolScheme
                    : platform.ProtocolScheme ?? "xbox:";

                // O protocolo oficial do Xbox App para Windows é "xbox:" (não direcionar para a loja na execução)
                if (platform.Id.Equals("xbox", StringComparison.OrdinalIgnoreCase) ||
                    storeUri.StartsWith("ms-windows-store:", StringComparison.OrdinalIgnoreCase))
                {
                    storeUri = "xbox:";
                }

                _logger.LogInformation("Iniciando aplicativo Store {Name} via protocolo {Protocol}", platform.Name, storeUri);

                var psi = new ProcessStartInfo
                {
                    FileName = storeUri,
                    UseShellExecute = true
                };

                var proc = Process.Start(psi);
                return new ExecutionResult(true, proc?.Id, isAlreadyRunning
                    ? "Aplicativo Store ativado em primeiro plano."
                    : "Iniciado com sucesso via protocolo do Windows.");
            }

            // 3. Obtém ou localiza o executável principal
            string? targetExe = platform.DetectedExecutablePath;
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            {
                _logger.LogWarning("Executável não encontrado em {Path}. Executando nova detecção para {Name}...", targetExe, platform.Name);

                var reDetectResult = await _detectionService.DetectAsync(platform, cancellationToken);
                if (reDetectResult.IsInstalled && !string.IsNullOrWhiteSpace(reDetectResult.ExecutablePath))
                {
                    targetExe = reDetectResult.ExecutablePath;
                    platform.DetectedExecutablePath = targetExe;
                    platform.DetectedVersion = reDetectResult.InstalledVersion;
                }
            }

            // 3.1 Fallback de varredura direta nos diretórios padrão caso o cache de detecção não esteja populado
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            {
                foreach (var dir in platform.Detection.DefaultInstallDirectories)
                {
                    try
                    {
                        var expandedDir = Environment.ExpandEnvironmentVariables(dir);
                        if (!Directory.Exists(expandedDir)) continue;

                        foreach (var exeName in platform.Detection.KnownExecutableNames)
                        {
                            var candidate = Path.Combine(expandedDir, exeName);
                            if (File.Exists(candidate))
                            {
                                targetExe = candidate;
                                platform.DetectedExecutablePath = targetExe;
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }

                    if (!string.IsNullOrWhiteSpace(targetExe) && File.Exists(targetExe))
                    {
                        break;
                    }
                }
            }

            // 4. Se não encontrar o executável físico, tenta ativação por protocolo registrado (ex: steam://, uplay://)
            if (string.IsNullOrWhiteSpace(targetExe) || !File.Exists(targetExe))
            {
                var fallbackProtocol = !string.IsNullOrWhiteSpace(platform.Execution?.ProtocolScheme)
                    ? platform.Execution.ProtocolScheme
                    : platform.ProtocolScheme;

                if (!string.IsNullOrWhiteSpace(fallbackProtocol))
                {
                    _logger.LogInformation("Executável não encontrado fisicamente; acionando protocolo registrado {Protocol} para {Name}...", fallbackProtocol, platform.Name);
                    var protoPsi = new ProcessStartInfo
                    {
                        FileName = fallbackProtocol,
                        UseShellExecute = true
                    };
                    var protoProc = Process.Start(protoPsi);
                    return new ExecutionResult(true, protoProc?.Id, "Launcher ativado via protocolo do sistema.");
                }

                return new ExecutionResult(false, null, "Executável do launcher não foi localizado no sistema após nova varredura.");
            }

            // 5. Se o launcher já estiver em execução e possuir protocolo registrado de interface, prioriza o protocolo para restaurar a janela
            if (isAlreadyRunning && !string.IsNullOrWhiteSpace(platform.Execution?.ProtocolScheme))
            {
                try
                {
                    _logger.LogInformation("Restaurando janela do launcher ativo {Name} via protocolo {Protocol}...", platform.Name, platform.Execution.ProtocolScheme);
                    var protoPsi = new ProcessStartInfo
                    {
                        FileName = platform.Execution.ProtocolScheme,
                        UseShellExecute = true
                    };
                    var protoProc = Process.Start(protoPsi);
                    return new ExecutionResult(true, protoProc?.Id, "Janela do launcher ativada e trazida para o primeiro plano.");
                }
                catch (Exception exProto)
                {
                    _logger.LogWarning(exProto, "Tentativa de ativação via protocolo falhou. Recorrendo à chamada do executável principal.");
                }
            }

            // 6. Execução padrão com ShellExecute e WorkingDirectory correto
            _logger.LogInformation("Disparando {Name} através de {Path}", platform.Name, targetExe);

            var startInfo = new ProcessStartInfo
            {
                FileName = targetExe,
                Arguments = platform.ExecutionArguments ?? platform.Execution?.DefaultArguments ?? string.Empty,
                WorkingDirectory = Path.GetDirectoryName(targetExe) ?? string.Empty,
                UseShellExecute = true
            };

            var process = Process.Start(startInfo);
            return new ExecutionResult(true, process?.Id, isAlreadyRunning
                ? "Launcher ativado e trazido para o primeiro plano."
                : "Launcher iniciado com sucesso.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar o launcher {LauncherName}", platform.Name);
            return new ExecutionResult(false, null, $"Falha ao iniciar: {ex.Message}");
        }
    }

    private static List<Process> GetMatchingProcesses(LauncherPlatform platform)
    {
        var result = new List<Process>();
        try
        {
            foreach (var exeName in platform.Detection.KnownExecutableNames)
            {
                var procName = Path.GetFileNameWithoutExtension(exeName);
                result.AddRange(Process.GetProcessesByName(procName));
            }
        }
        catch
        {
        }
        return result;
    }
}
