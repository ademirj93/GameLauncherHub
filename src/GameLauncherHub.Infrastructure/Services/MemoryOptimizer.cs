using System.Diagnostics;
using System.Runtime;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Otimizador avançado de consumo de memória RAM para a aplicação Windows Desktop.
/// Executa coleta agressiva de lixo com compactação do Large Object Heap (LOH) de forma controlada.
/// </summary>
public static class MemoryOptimizer
{
    /// <summary>
    /// Executa coleta de lixo e compactação de memória em segundo plano sem causar tempestade de page faults.
    /// </summary>
    public static void TrimMemory()
    {
        try
        {
            // 1. Compacta Large Object Heap se houver fragmentação
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;

            // 2. Coleta de todas as gerações do GC
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: false, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: false, compacting: true);
        }
        catch
        {
        }
    }

    /// <summary>
    /// Agenda otimização de memória assíncrona após estabilização do carregamento inicial.
    /// </summary>
    public static void SchedulePostStartupTrim(int delayMilliseconds = 2500)
    {
        Task.Delay(delayMilliseconds).ContinueWith(_ =>
        {
            TrimMemory();
        }, TaskScheduler.Default);
    }
}
