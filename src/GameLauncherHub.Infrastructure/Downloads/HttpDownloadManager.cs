using System.Diagnostics;
using System.IO;
using System.Net.Http;
using GameLauncherHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.Downloads;

/// <summary>
/// Gerenciador de downloads assíncrono para instaladores oficiais com acompanhamento de progresso,
/// cálculo de velocidade de transferência, cancelamento e limpeza de arquivos parciais.
/// </summary>
public class HttpDownloadManager : IDownloadManager
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpDownloadManager>? _logger;
    private const int BufferSize = 81920; // 80 KB buffer

    public HttpDownloadManager(HttpClient? httpClient = null, ILogger<HttpDownloadManager>? logger = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(30),
            AutomaticDecompression = System.Net.DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromMinutes(30)
        };
    }

    public async Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string targetFilePath,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken = default)
    {
        var targetDir = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrWhiteSpace(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var tempPartFilePath = targetFilePath + ".part";

        try
        {
            _logger?.LogInformation("Iniciando download de {Url} para {Target}", downloadUrl, targetFilePath);

            using var response = await _httpClient.GetAsync(
                downloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                tempPartFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous
            );

            var buffer = new byte[BufferSize];
            long totalBytesRead = 0;
            int bytesRead;

            var stopwatch = Stopwatch.StartNew();
            long bytesReadInInterval = 0;
            var intervalStopwatch = Stopwatch.StartNew();
            string speedText = "0 KB/s";

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                totalBytesRead += bytesRead;
                bytesReadInInterval += bytesRead;

                // Atualiza o cálculo de velocidade a cada 500ms
                if (intervalStopwatch.ElapsedMilliseconds >= 500)
                {
                    var seconds = intervalStopwatch.Elapsed.TotalSeconds;
                    if (seconds > 0)
                    {
                        var bytesPerSec = bytesReadInInterval / seconds;
                        speedText = FormatTransferSpeed(bytesPerSec);
                    }
                    bytesReadInInterval = 0;
                    intervalStopwatch.Restart();
                }

                double percentage = totalBytes.HasValue && totalBytes.Value > 0
                    ? Math.Round((double)totalBytesRead / totalBytes.Value * 100.0, 1)
                    : 0.0;

                string status = totalBytes.HasValue
                    ? $"{FormatBytes(totalBytesRead)} de {FormatBytes(totalBytes.Value)}"
                    : $"{FormatBytes(totalBytesRead)} recebidos";

                progress.Report(new DownloadProgressInfo(
                    Percentage: percentage,
                    BytesReceived: totalBytesRead,
                    TotalBytesToReceive: totalBytes,
                    TransferSpeedText: speedText,
                    StatusText: status
                ));
            }

            stopwatch.Stop();
            await fileStream.FlushAsync(cancellationToken);
            fileStream.Close();

            // Renomeia o arquivo temporário .part para o nome final
            if (File.Exists(targetFilePath))
            {
                File.Delete(targetFilePath);
            }
            File.Move(tempPartFilePath, targetFilePath);

            progress.Report(new DownloadProgressInfo(
                Percentage: 100,
                BytesReceived: totalBytesRead,
                TotalBytesToReceive: totalBytesRead,
                TransferSpeedText: "Concluído",
                StatusText: "Download finalizado"
            ));

            _logger?.LogInformation("Download concluído com sucesso ({Size} bytes) em {Elapsed}s",
                totalBytesRead, stopwatch.Elapsed.TotalSeconds);

            return targetFilePath;
        }
        catch (OperationCanceledException)
        {
            _logger?.LogWarning("Download cancelado pelo usuário.");
            CleanupFile(tempPartFilePath);
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Falha durante o download de {Url}", downloadUrl);
            CleanupFile(tempPartFilePath);
            throw;
        }
    }

    private static void CleanupFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignora erro de limpeza de arquivo temporário
        }
    }

    private static string FormatTransferSpeed(double bytesPerSecond)
    {
        if (bytesPerSecond >= 1024 * 1024)
        {
            return $"{(bytesPerSecond / (1024 * 1024)):F1} MB/s";
        }
        return $"{(bytesPerSecond / 1024):F0} KB/s";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
        {
            return $"{(double)bytes / (1024 * 1024 * 1024):F2} GB";
        }
        if (bytes >= 1024 * 1024)
        {
            return $"{(double)bytes / (1024 * 1024):F1} MB";
        }
        if (bytes >= 1024)
        {
            return $"{(double)bytes / 1024:F0} KB";
        }
        return $"{bytes} B";
    }
}

