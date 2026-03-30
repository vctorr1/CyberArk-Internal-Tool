using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberArkManager.Models;

namespace CyberArkManager.Services;

/// <summary>
/// High-level wrapper for CyberArk PVWA REST API v2.
/// All methods require an active session (AuthService.LoginAsync called first).
/// </summary>
public class CyberArkApiService
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public CyberArkApiService(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    // ── Convenience ──────────────────────────────────────────────────────────

    private string BaseUrl => _auth.CurrentSession?.PvwaUrl
        ?? throw new InvalidOperationException("No hay sesión activa.");

    // ═══════════════════════════════════════════════════════════════════════
    // ACCOUNTS
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Retrieves all accounts with optional filtering.
    /// Automatically follows pagination (nextLink).
    /// </summary>
    public async Task<List<Account>> GetAllAccountsAsync(
        string? safeName = null,
        string? searchKeyword = null,
        CancellationToken ct = default)
    {
        var results = new List<Account>();
        var query = BuildAccountQuery(safeName, searchKeyword, limit: 100, offset: 0);
        var url = $"{BaseUrl}/API/Accounts{query}";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _http.GetAsync(url, ct);
            await EnsureSuccess(response, ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<AccountsResponse>(json, JsonOpts);

            if (page?.Value is not null)
                results.AddRange(page.Value);

            // Follow pagination
            url = page?.NextLink;
            if (!string.IsNullOrEmpty(url) && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = $"{BaseUrl}{url}";
        }

        return results;
    }

    /// <summary>Gets a single account by its ID.</summary>
    public async Task<Account> GetAccountAsync(string accountId, CancellationToken ct = default)
    {
        var response = await _http.GetAsync($"{BaseUrl}/API/Accounts/{accountId}", ct);
        await EnsureSuccess(response, ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<Account>(json, JsonOpts)
               ?? throw new InvalidOperationException("Respuesta vacía del servidor.");
    }

    /// <summary>Creates a new account.</summary>
    public async Task<Account> CreateAccountAsync(AccountCreateRequest request, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(request, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync($"{BaseUrl}/API/Accounts", content, ct);
        await EnsureSuccess(response, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<Account>(responseJson, JsonOpts)
               ?? throw new InvalidOperationException("El servidor no devolvió la cuenta creada.");
    }

    /// <summary>
    /// Updates specific account properties using PATCH.
    /// Only the provided properties are changed.
    /// </summary>
    public async Task<Account> PatchAccountAsync(
        string accountId,
        List<PatchOperation> operations,
        CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(operations, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Patch, $"{BaseUrl}/API/Accounts/{accountId}")
        {
            Content = content
        };
        var response = await _http.SendAsync(request, ct);
        await EnsureSuccess(response, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<Account>(responseJson, JsonOpts)
               ?? throw new InvalidOperationException("El servidor no devolvió la cuenta actualizada.");
    }

    /// <summary>Deletes an account by ID.</summary>
    public async Task DeleteAccountAsync(string accountId, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"{BaseUrl}/API/Accounts/{accountId}", ct);
        await EnsureSuccess(response, ct);
    }

    /// <summary>
    /// Triggers an immediate password change (rotate) for an account.
    /// POST /API/Accounts/{id}/Change
    /// </summary>
    public async Task RotatePasswordAsync(string accountId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"{BaseUrl}/API/Accounts/{accountId}/Change", null, ct);
        await EnsureSuccess(response, ct);
    }

    /// <summary>
    /// Triggers password verification for an account.
    /// </summary>
    public async Task VerifyPasswordAsync(string accountId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"{BaseUrl}/API/Accounts/{accountId}/Verify", null, ct);
        await EnsureSuccess(response, ct);
    }

    /// <summary>
    /// Triggers password reconciliation for an account.
    /// </summary>
    public async Task ReconcilePasswordAsync(string accountId, CancellationToken ct = default)
    {
        var response = await _http.PostAsync(
            $"{BaseUrl}/API/Accounts/{accountId}/Reconcile", null, ct);
        await EnsureSuccess(response, ct);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SAFES
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Retrieves all Safes the current user has access to.</summary>
    public async Task<List<Safe>> GetAllSafesAsync(CancellationToken ct = default)
    {
        var results = new List<Safe>();
        var url = $"{BaseUrl}/API/Safes?limit=100&offset=0";

        while (!string.IsNullOrEmpty(url))
        {
            var response = await _http.GetAsync(url, ct);
            await EnsureSuccess(response, ct);

            var json = await response.Content.ReadAsStringAsync(ct);
            var page = JsonSerializer.Deserialize<SafesResponse>(json, JsonOpts);

            if (page?.Value is not null)
                results.AddRange(page.Value);

            // CyberArk safes endpoint doesn't always return nextLink — handle count-based pagination
            if (page?.Value?.Count < 100)
                break;

            var offset = results.Count;
            url = $"{BaseUrl}/API/Safes?limit=100&offset={offset}";
        }

        return results;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BULK ACCOUNT CREATION (Direct API Upload)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Creates multiple accounts sequentially via API.
    /// Reports progress per account.
    /// </summary>
    public async Task<BulkUploadResult> BulkCreateAccountsAsync(
        IEnumerable<AccountCreateRequest> accounts,
        IProgress<BulkUploadProgress>? progress = null,
        CancellationToken ct = default)
    {
        var list = accounts.ToList();
        var result = new BulkUploadResult { Total = list.Count };
        int current = 0;

        foreach (var account in list)
        {
            ct.ThrowIfCancellationRequested();
            current++;

            try
            {
                await CreateAccountAsync(account, ct);
                result.Succeeded++;
                progress?.Report(new BulkUploadProgress
                {
                    Current = current,
                    Total = list.Count,
                    LastAccountName = $"{account.UserName}@{account.Address}",
                    Success = true
                });
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"[{account.UserName}@{account.Address}]: {ex.Message}");
                progress?.Report(new BulkUploadProgress
                {
                    Current = current,
                    Total = list.Count,
                    LastAccountName = $"{account.UserName}@{account.Address}",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }

            // Small delay to avoid overwhelming the API
            await Task.Delay(150, ct);
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════════

    private static string BuildAccountQuery(string? safeName, string? search, int limit, int offset)
    {
        var parts = new List<string> { $"limit={limit}", $"offset={offset}" };
        if (!string.IsNullOrWhiteSpace(safeName))
            parts.Add($"filter=safeName eq {Uri.EscapeDataString(safeName)}");
        if (!string.IsNullOrWhiteSpace(search))
            parts.Add($"search={Uri.EscapeDataString(search)}");
        return "?" + string.Join("&", parts);
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;

        var body = await response.Content.ReadAsStringAsync(ct);
        var code = (int)response.StatusCode;

        string message = code switch
        {
            401 => "No autorizado — el token puede haber expirado.",
            403 => "Acceso denegado — permisos insuficientes.",
            404 => "Recurso no encontrado.",
            409 => "Conflicto — la cuenta puede ya existir.",
            _ => ParseApiError(body)
        };

        throw new CyberArkApiException(code, message, body);
    }

    private static string ParseApiError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("ErrorMessage", out var msg))
                return msg.GetString() ?? body;
            if (doc.RootElement.TryGetProperty("Details", out var det))
                return det.GetString() ?? body;
            if (doc.RootElement.TryGetProperty("message", out var m))
                return m.GetString() ?? body;
        }
        catch { }
        return body.Length > 300 ? body[..300] : body;
    }
}

// ── Supporting types ──────────────────────────────────────────────────────

public class PatchOperation
{
    [JsonPropertyName("op")]
    public string Op { get; set; } = "replace";

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}

public class BulkUploadResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Failed > 0;
}

public class BulkUploadProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string LastAccountName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double PercentComplete => Total == 0 ? 0 : (double)Current / Total * 100;
}

public class CyberArkApiException : Exception
{
    public int StatusCode { get; }
    public string RawBody { get; }

    public CyberArkApiException(int statusCode, string message, string rawBody)
        : base(message)
    {
        StatusCode = statusCode;
        RawBody = rawBody;
    }
}
