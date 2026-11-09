using System.IO;
using System.Text.Json;
using GameLauncherHub.Domain.Interfaces;
using GameLauncherHub.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Services;

/// <summary>
/// Serviço de histórico e auditoria de operações em formato JSON local.
/// </summary>
public class OperationHistoryService : IOperationHistoryService
{
    private readonly ILogger<OperationHistoryService> _logger;
    private readonly string _historyFilePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public OperationHistoryService(ILogger<OperationHistoryService> logger)
    {
        _logger = logger;
        var appFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GameLauncherHub");
        Directory.CreateDirectory(appFolder);
        _historyFilePath = Path.Combine(appFolder, "history.json");
    }

    public async Task LogAsync(OperationLog log, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var logs = (await LoadLogsInternalAsync(cancellationToken)).ToList();
            logs.Insert(0, log);

            // Mantém os últimos 500 registros para evitar crescimento descontrolado
            if (logs.Count > 500)
            {
                logs = logs.Take(500).ToList();
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            await using var stream = File.Create(_historyFilePath);
            await JsonSerializer.SerializeAsync(stream, logs, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gravar registro de auditoria no histórico.");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<OperationLog>> GetRecentLogsAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var logs = await LoadLogsInternalAsync(cancellationToken);
            return logs.Take(count).ToList().AsReadOnly();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_historyFilePath))
            {
                File.Delete(_historyFilePath);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<List<OperationLog>> LoadLogsInternalAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_historyFilePath))
            return new List<OperationLog>();

        try
        {
            await using var stream = File.OpenRead(_historyFilePath);
            var logs = await JsonSerializer.DeserializeAsync<List<OperationLog>>(stream, cancellationToken: cancellationToken);
            return logs ?? new List<OperationLog>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao ler arquivo de histórico existente. Criando nova lista.");
            return new List<OperationLog>();
        }
    }
}

