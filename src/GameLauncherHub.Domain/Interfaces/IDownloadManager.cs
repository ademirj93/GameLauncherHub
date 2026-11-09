namespace GameLauncherHub.Domain.Interfaces;

/// <summary>
/// Contrato do gerenciador de downloads assíncronos.
/// </summary>
public interface IDownloadManager
{
    Task<string> DownloadInstallerAsync(
        string downloadUrl,
        string targetFileName,
        IProgress<DownloadProgressInfo> progress,
        CancellationToken cancellationToken = default
    );
}

public record DownloadProgressInfo(
    double Percentage,
    long BytesReceived,
    long? TotalBytesToReceive,
    string TransferSpeedText,
    string StatusText
);

