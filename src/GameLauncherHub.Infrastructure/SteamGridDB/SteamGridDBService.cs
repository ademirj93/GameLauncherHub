using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using GameLauncherHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameLauncherHub.Infrastructure.SteamGridDB;

/// <summary>
/// Cliente de integração com a API oficial v2 do SteamGridDB (https://www.steamgriddb.com/api/v2).
/// Consulta ícones de alta qualidade para launchers de jogos com autenticação por chave de API.
/// </summary>
public class SteamGridDBService : ISteamGridDBService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SteamGridDBService>? _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private const string BaseApiUrl = "https://www.steamgriddb.com/api/v2";

    public SteamGridDBService(HttpClient? httpClient = null, ILogger<SteamGridDBService>? logger = null)
    {
        _logger = logger;
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return false;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseApiUrl}/search/autocomplete/Steam");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Validação da API Key do SteamGridDB falhou com status {Code}", response.StatusCode);
                return false;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("success", out var successProp))
            {
                return successProp.GetBoolean();
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro de rede ao validar API Key do SteamGridDB.");
            return false;
        }
    }

    public async Task<string?> SearchAndGetIconUrlAsync(
        string launcherName,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(launcherName) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        try
        {
            // Termos de busca otimizados para encontrar os apps oficiais no SteamGridDB
            var searchTerm = GetSearchQueryForLauncher(launcherName);
            var gameId = await SearchGameIdAsync(searchTerm, apiKey, cancellationToken);

            if (!gameId.HasValue)
            {
                _logger?.LogInformation("Nenhum registro correspondente a '{SearchTerm}' foi encontrado no SteamGridDB.", searchTerm);
                return null;
            }

            return await GetBestIconUrlAsync(gameId.Value, apiKey, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erro ao obter ícone do SteamGridDB para {Launcher}", launcherName);
            return null;
        }
    }

    private async Task<int?> SearchGameIdAsync(string query, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"{BaseApiUrl}/search/autocomplete/{Uri.EscapeDataString(query)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
        {
            return null;
        }

        if (!doc.RootElement.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        // Procura correspondência exata ou a mais próxima no resultado
        int? firstId = null;
        foreach (var item in dataProp.EnumerateArray())
        {
            if (item.TryGetProperty("id", out var idProp) && item.TryGetProperty("name", out var nameProp))
            {
                var id = idProp.GetInt32();
                var name = nameProp.GetString() ?? "";

                if (firstId == null) firstId = id;

                // Correspondência estrita
                if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    return id;
                }
            }
        }

        return firstId;
    }

    private async Task<string?> GetBestIconUrlAsync(int gameId, string apiKey, CancellationToken cancellationToken)
    {
        var url = $"{BaseApiUrl}/icons/game/{gameId}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("success", out var successProp) || !successProp.GetBoolean())
        {
            return null;
        }

        if (!doc.RootElement.TryGetProperty("data", out var dataProp) || dataProp.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        string? bestUrl = null;
        int highestScore = int.MinValue;

        foreach (var icon in dataProp.EnumerateArray())
        {
            // Ignora conteúdo adulto (NSFW) ou humor
            if (icon.TryGetProperty("nsfw", out var nsfw) && nsfw.GetBoolean()) continue;
            if (icon.TryGetProperty("humor", out var humor) && humor.GetBoolean()) continue;

            int score = icon.TryGetProperty("score", out var scoreProp) ? scoreProp.GetInt32() : 0;
            string style = icon.TryGetProperty("style", out var styleProp) ? styleProp.GetString() ?? "" : "";
            string iconUrl = icon.TryGetProperty("url", out var urlProp) ? urlProp.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(iconUrl)) continue;

            // Bônus se marcado como 'official'
            if (string.Equals(style, "official", StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }

            if (score > highestScore)
            {
                highestScore = score;
                bestUrl = iconUrl;
            }
        }

        return bestUrl;
    }

    private static string GetSearchQueryForLauncher(string launcherName)
    {
        var clean = launcherName.Trim();
        if (clean.StartsWith("Epic", StringComparison.OrdinalIgnoreCase)) return "Epic Games";
        if (clean.StartsWith("Amazon", StringComparison.OrdinalIgnoreCase)) return "Amazon Games";
        if (clean.StartsWith("Battle", StringComparison.OrdinalIgnoreCase)) return "Battle.net";
        if (clean.StartsWith("Ubisoft", StringComparison.OrdinalIgnoreCase)) return "Ubisoft Connect";
        if (clean.StartsWith("EA", StringComparison.OrdinalIgnoreCase)) return "EA App";
        if (clean.StartsWith("Heroic", StringComparison.OrdinalIgnoreCase)) return "Heroic Games Launcher";
        if (clean.StartsWith("GOG", StringComparison.OrdinalIgnoreCase)) return "GOG Galaxy";
        if (clean.StartsWith("Xbox", StringComparison.OrdinalIgnoreCase)) return "Xbox";
        return clean;
    }
}

