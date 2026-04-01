using System.Net;
using System.Net.Http;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CyberArkManager.Models;
using Polly;
using Polly.Extensions.Http;
using Polly.Retry;
using Serilog;

namespace CyberArkManager.Services;

/// <summary>
/// Complete wrapper for CyberArk PVWA REST API v2 â€” all official endpoints.
/// Improvements over v1:
///   â€¢ Polly retry policy (3 attempts, exponential back-off) for transient HTTP errors.
///   â€¢ Per-request timeout via CancellationTokenSource (30 s default).
///   â€¢ Structured audit logging via Serilog for all mutating operations.
///   â€¢ HTTP client layer separated from DTO mapping via GetJson / PostJson helpers.
/// </summary>
public class CyberArkApiService
{
    // â”€â”€ Constants â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);
    private const int RetryCount = 3;
    private const int MaxBulkParallelism = 4;
    private const int MaxLinkParallelism = 4;

    // â”€â”€ Dependencies â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private readonly HttpClient  _http;
    private readonly AuthService _auth;
    private static readonly ILogger _log = Log.ForContext<CyberArkApiService>();

    private static readonly JsonSerializerOptions J = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Polly retry policy: retries on 5xx and network errors, with exponential back-off.
    /// Does NOT retry on 401/403/404/409 â€” those are logic errors, not transient failures.
    /// </summary>
    private static readonly AsyncRetryPolicy<HttpResponseMessage> RetryPolicy =
        HttpPolicyExtensions
            .HandleTransientHttpError()                            // 5xx + HttpRequestException
            .OrResult(r => (int)r.StatusCode >= 500)              // explicit 5xx guard
            .WaitAndRetryAsync(
                RetryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)), // 2s, 4s, 8s
                onRetry: (outcome, delay, attempt, _) =>
                {
                    var status = outcome.Result?.StatusCode.ToString() ?? outcome.Exception?.GetType().Name;
                    Log.Warning("Retry {Attempt}/{Max} after {Delay}s â€” {Status}",
                        attempt, RetryCount, delay.TotalSeconds, status);
                });

    public CyberArkApiService(HttpClient http, AuthService auth) { _http = http; _auth = auth; }

    private string Base => _auth.CurrentSession?.PvwaUrl
        ?? throw new InvalidOperationException("No hay sesiÃ³n activa. Inicia sesiÃ³n primero.");

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ACCOUNTS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public virtual async Task<List<Account>> GetAccountsAsync(string? safe = null, string? search = null,
        string? searchType = null, string? sort = null, CancellationToken ct = default)
    {
        var results = new List<Account>();
        var q = new List<string> { "limit=100", "offset=0" };
        if (!string.IsNullOrWhiteSpace(safe))   q.Add($"filter=safeName eq {Uri.EscapeDataString(safe)}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"search={Uri.EscapeDataString(search)}");
        if (!string.IsNullOrWhiteSpace(searchType)) q.Add($"searchType={searchType}");
        if (!string.IsNullOrWhiteSpace(sort))   q.Add($"sort={sort}");

        string? url = $"{Base}/API/Accounts?{string.Join("&", q)}";
        while (!string.IsNullOrEmpty(url))
        {
            var r = await GetJson<AccountsResponse>(url, ct);
            if (r?.Value is not null) results.AddRange(r.Value);
            url = string.IsNullOrEmpty(r?.NextLink) ? null
                : (r!.NextLink!.StartsWith("http") ? r.NextLink : Base + r.NextLink);
        }
        _log.Debug("GetAccounts returned {Count} accounts. Safe={Safe}", results.Count, safe ?? "*");
        return results;
    }

    public async Task<Account> GetAccountAsync(string id, CancellationToken ct = default)
        => await GetJson<Account>($"{Base}/API/Accounts/{id}", ct)
           ?? throw new InvalidOperationException("Cuenta no encontrada.");

    public async Task<Account> CreateAccountAsync(AccountCreateRequest req, CancellationToken ct = default)
    {
        _log.Information("CreateAccount requested.");
        return await PostJson<Account>($"{Base}/API/Accounts", req, ct)
               ?? throw new InvalidOperationException("Error al crear la cuenta.");
    }

    public async Task<Account> PatchAccountAsync(string id, List<PatchOperation> ops, CancellationToken ct = default)
    {
        _log.Information("PatchAccount. Id={Id} Ops={Count}", id, ops.Count);
        var payload = JsonSerializer.Serialize(ops, J);
        var r = await SendWithRetry(
            () => new HttpRequestMessage(HttpMethod.Patch, $"{Base}/API/Accounts/{id}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            },
            ct);
        await EnsureOk(r, ct);
        return JsonSerializer.Deserialize<Account>(await r.Content.ReadAsStringAsync(ct), J)!;
    }

    public async Task DeleteAccountAsync(string id, CancellationToken ct = default)
    {
        _log.Warning("DeleteAccount. Id={Id}", id);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/Accounts/{id}", timeoutToken), ct), ct);
    }

    // â€” Password Operations â€”
    public async Task ChangePasswordAsync(string id, CancellationToken ct = default)
    {
        _log.Information("ChangePassword triggered. AccountId={Id}", id);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Accounts/{id}/Change", null, timeoutToken), ct), ct);
    }

    public async Task VerifyPasswordAsync(string id, CancellationToken ct = default)
        => await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Accounts/{id}/Verify", null, timeoutToken), ct), ct);

    public async Task ReconcilePasswordAsync(string id, CancellationToken ct = default)
    {
        _log.Information("ReconcilePassword. AccountId={Id}", id);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Accounts/{id}/Reconcile", null, timeoutToken), ct), ct);
    }

    public async Task SetNextPasswordAsync(string id, string newPassword, bool changeImmediately = false, CancellationToken ct = default)
    {
        _log.Information("SetNextPassword. AccountId={Id} Immediate={Immediate}", id, changeImmediately);
        await EnsureOk(await PostRaw($"{Base}/API/Accounts/{id}/SetNextPassword",
            new { ChangeImmediately = changeImmediately, NewCredentials = newPassword }, ct), ct);
    }

    public async Task<string> GetPasswordValueAsync(string id, string reason, string ticketId = "",
        string ticketingSystem = "", int connectionMode = 0, CancellationToken ct = default)
    {
        _log.Warning("PasswordRetrieval. AccountId={Id}", id);
        var payload = new { reason, TicketId = ticketId, TicketingSystemName = ticketingSystem, ConnectionTypeRequest = connectionMode };
        var r = await PostRaw($"{Base}/API/Accounts/{id}/Password/Retrieve", payload, ct);
        await EnsureOk(r, ct);
        return (await r.Content.ReadAsStringAsync(ct)).Trim('"');
    }

    // â€” Check-in / Check-out â€”
    public async Task CheckOutAsync(string id, string reason = "", CancellationToken ct = default)
    {
        _log.Information("CheckOut. AccountId={Id}", id);
        await EnsureOk(await PostRaw($"{Base}/API/Accounts/{id}/CheckOut", new { reason }, ct), ct);
    }

    public async Task CheckInAsync(string id, CancellationToken ct = default)
    {
        _log.Information("CheckIn. AccountId={Id}", id);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Accounts/{id}/CheckIn", null, timeoutToken), ct), ct);
    }

    // â€” Account Groups â€”
    public virtual async Task LinkAccountAsync(string id, string extraPassId, int extraPassIndex, CancellationToken ct = default)
        => await EnsureOk(await PostRaw($"{Base}/API/Accounts/{id}/LinkAccount",
            new { extraPasswordIndex = extraPassIndex, linked = true, associatedLinkedAccountId = extraPassId }, ct), ct);

    public async Task UnlinkAccountAsync(string id, int extraPassIndex, CancellationToken ct = default)
        => await EnsureOk(await PostRaw($"{Base}/API/Accounts/{id}/LinkAccount",
            new { extraPasswordIndex = extraPassIndex, linked = false }, ct), ct);

    // â€” Activity Log â€”
    public async Task<List<AccountActivityLog>> GetAccountActivitiesAsync(string id, CancellationToken ct = default)
    {
        var r = await GetJson<AccountActivitiesResponse>($"{Base}/API/Accounts/{id}/Activities", ct);
        return r?.Activities ?? new();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SAFES
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<Safe>> GetSafesAsync(string? search = null, CancellationToken ct = default)
    {
        var results = new List<Safe>();
        var q = "limit=100&offset=0" + (string.IsNullOrWhiteSpace(search) ? "" : $"&search={Uri.EscapeDataString(search)}");
        string? url = $"{Base}/API/Safes?{q}";
        while (!string.IsNullOrEmpty(url))
        {
            var r = await GetJson<SafesResponse>(url, ct);
            if (r?.Value is not null) results.AddRange(r.Value);
            if (r?.Value?.Count < 100) break;
            url = string.IsNullOrEmpty(r?.NextLink) ? null : (r!.NextLink!.StartsWith("http") ? r.NextLink : Base + r.NextLink);
        }
        return results;
    }

    public async Task<Safe> GetSafeAsync(string safeUrlId, CancellationToken ct = default)
        => await GetJson<Safe>($"{Base}/API/Safes/{safeUrlId}", ct) ?? throw new InvalidOperationException("Safe no encontrado.");

    public async Task<Safe> CreateSafeAsync(SafeCreateRequest req, CancellationToken ct = default)
    {
        _log.Information("CreateSafe requested.");
        return await PostJson<Safe>($"{Base}/API/Safes", req, ct) ?? throw new InvalidOperationException("Error al crear el Safe.");
    }

    public async Task<Safe> UpdateSafeAsync(string safeUrlId, SafeCreateRequest req, CancellationToken ct = default)
    {
        _log.Information("UpdateSafe. SafeId={Id}", safeUrlId);
        var payload = JsonSerializer.Serialize(req, J);
        var resp = await SendWithRetry(
            () => new HttpRequestMessage(HttpMethod.Put, $"{Base}/API/Safes/{safeUrlId}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            },
            ct);
        await EnsureOk(resp, ct);
        return JsonSerializer.Deserialize<Safe>(await resp.Content.ReadAsStringAsync(ct), J)!;
    }

    public async Task DeleteSafeAsync(string safeUrlId, CancellationToken ct = default)
    {
        _log.Warning("DeleteSafe. SafeId={Id}", safeUrlId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/Safes/{safeUrlId}", timeoutToken), ct), ct);
    }

    // â€” Safe Members â€”
    public async Task<List<SafeMember>> GetSafeMembersAsync(string safeUrlId, CancellationToken ct = default)
    {
        var results = new List<SafeMember>();
        string? url = $"{Base}/API/Safes/{safeUrlId}/Members?limit=100";
        while (!string.IsNullOrEmpty(url))
        {
            var r = await GetJson<SafeMembersResponse>(url, ct);
            if (r?.Value is not null) results.AddRange(r.Value);
            url = string.IsNullOrEmpty(r?.NextLink) ? null : (r!.NextLink!.StartsWith("http") ? r.NextLink : Base + r.NextLink);
        }
        return results;
    }

    public async Task<SafeMember> AddSafeMemberAsync(string safeUrlId, string memberName, string memberType, SafePermissions perms, CancellationToken ct = default)
    {
        _log.Information("AddSafeMember requested.");
        return await PostJson<SafeMember>($"{Base}/API/Safes/{safeUrlId}/Members",
                   new { memberName, memberType, permissions = perms }, ct)
               ?? throw new InvalidOperationException("Error al aÃ±adir miembro.");
    }

    public async Task<SafeMember> UpdateSafeMemberAsync(string safeUrlId, string memberName, SafePermissions perms, CancellationToken ct = default)
    {
        _log.Information("UpdateSafeMember requested.");
        var payload = JsonSerializer.Serialize(new { permissions = perms }, J);
        var resp = await SendWithRetry(
            () => new HttpRequestMessage(HttpMethod.Put, $"{Base}/API/Safes/{safeUrlId}/Members/{memberName}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            },
            ct);
        await EnsureOk(resp, ct);
        return JsonSerializer.Deserialize<SafeMember>(await resp.Content.ReadAsStringAsync(ct), J)!;
    }

    public async Task DeleteSafeMemberAsync(string safeUrlId, string memberName, CancellationToken ct = default)
    {
        _log.Warning("DeleteSafeMember requested.");
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/Safes/{safeUrlId}/Members/{memberName}", timeoutToken), ct), ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PLATFORMS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<Platform>> GetPlatformsAsync(bool? active = null, string? search = null, CancellationToken ct = default)
    {
        var q = new List<string>();
        if (active.HasValue) q.Add($"Active={active.Value.ToString().ToLower()}");
        if (!string.IsNullOrWhiteSpace(search)) q.Add($"Search={Uri.EscapeDataString(search)}");
        var url = $"{Base}/API/Platforms" + (q.Count > 0 ? "?" + string.Join("&", q) : "");
        var resp = await GetJson<PlatformsResponse>(url, ct);
        return resp?.Platforms?.Select(p => p.General!).Where(p => p is not null).ToList() ?? new();
    }

    public async Task<PlatformWrapper> GetPlatformAsync(string platformId, CancellationToken ct = default)
        => await GetJson<PlatformWrapper>($"{Base}/API/Platforms/{platformId}", ct)
           ?? throw new InvalidOperationException("Plataforma no encontrada.");

    public async Task ActivatePlatformAsync(string platformId, CancellationToken ct = default)
        => await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Platforms/{platformId}/activate", null, timeoutToken), ct), ct);

    public async Task DeactivatePlatformAsync(string platformId, CancellationToken ct = default)
        => await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Platforms/{platformId}/deactivate", null, timeoutToken), ct), ct);

    public async Task DuplicatePlatformAsync(string platformId, string newName, string? description = null, CancellationToken ct = default)
        => await EnsureOk(await PostRaw($"{Base}/API/Platforms/{platformId}/duplicate",
            new { Name = newName, Description = description ?? "" }, ct), ct);

    public async Task DeletePlatformAsync(string platformId, CancellationToken ct = default)
    {
        _log.Warning("DeletePlatform. PlatformId={Id}", platformId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/Platforms/{platformId}", timeoutToken), ct), ct);
    }

    public async Task<byte[]> ExportPlatformAsync(string platformId, CancellationToken ct = default)
    {
        var r = await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Platforms/{platformId}/Export", null, timeoutToken), ct);
        await EnsureOk(r, ct);
        return await r.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task ImportPlatformAsync(byte[] zipData, CancellationToken ct = default)
    {
        _log.Information("ImportPlatform. SizeBytes={Size}", zipData.Length);
        var r = await ExecuteWithRetry(async timeoutToken =>
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(zipData), "file", "platform.zip");
            return await _http.PostAsync($"{Base}/API/Platforms/Import", form, timeoutToken);
        }, ct);
        await EnsureOk(r, ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // USERS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<CyberArkUser>> GetUsersAsync(string? filter = null, string? userType = null, CancellationToken ct = default)
    {
        var q = new List<string> { "limit=100" };
        if (!string.IsNullOrWhiteSpace(filter))   q.Add($"filter={Uri.EscapeDataString(filter)}");
        if (!string.IsNullOrWhiteSpace(userType)) q.Add($"UserType={userType}");
        var r = await GetJson<UsersResponse>($"{Base}/API/Users?{string.Join("&", q)}", ct);
        return r?.Users ?? new();
    }

    public async Task<CyberArkUser> GetUserAsync(int userId, CancellationToken ct = default)
        => await GetJson<CyberArkUser>($"{Base}/API/Users/{userId}", ct)
           ?? throw new InvalidOperationException("Usuario no encontrado.");

    public async Task<CyberArkUser> CreateUserAsync(UserCreateRequest req, CancellationToken ct = default)
    {
        _log.Information("CreateUser requested.");
        return await PostJson<CyberArkUser>($"{Base}/API/Users", req, ct)
               ?? throw new InvalidOperationException("Error al crear usuario.");
    }

    public async Task<CyberArkUser> UpdateUserAsync(int userId, object updateData, CancellationToken ct = default)
    {
        _log.Information("UpdateUser. UserId={Id}", userId);
        var payload = JsonSerializer.Serialize(updateData, J);
        var r = await SendWithRetry(
            () => new HttpRequestMessage(HttpMethod.Put, $"{Base}/API/Users/{userId}")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            },
            ct);
        await EnsureOk(r, ct);
        return JsonSerializer.Deserialize<CyberArkUser>(await r.Content.ReadAsStringAsync(ct), J)!;
    }

    public async Task DeleteUserAsync(int userId, CancellationToken ct = default)
    {
        _log.Warning("DeleteUser. UserId={Id}", userId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/Users/{userId}", timeoutToken), ct), ct);
    }

    public async Task ActivateUserAsync(int userId, CancellationToken ct = default)
        => await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.PostAsync($"{Base}/API/Users/{userId}/Activate", null, timeoutToken), ct), ct);

    public async Task SuspendUserAsync(int userId, CancellationToken ct = default)
        => await EnsureOk(await PostRaw($"{Base}/API/Users/{userId}/Disable", new { }, ct), ct);

    public async Task ResetUserPasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        _log.Warning("ResetUserPassword. UserId={Id}", userId);
        await EnsureOk(await PostRaw($"{Base}/API/Users/{userId}/ResetPassword",
            new { id = userId, NewPassword = newPassword }, ct), ct);
    }

    public async Task AddUserToGroupAsync(int groupId, string username, CancellationToken ct = default)
    {
        _log.Information("AddUserToGroup requested. GroupId={Group}", groupId);
        await EnsureOk(await PostRaw($"{Base}/API/UserGroups/{groupId}/Members",
            new { memberId = username }, ct), ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // USER GROUPS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<UserGroup>> GetGroupsAsync(string? filter = null, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(filter) ? "" : $"?filter={Uri.EscapeDataString(filter)}";
        var r = await GetJson<GroupsResponse>($"{Base}/API/UserGroups{q}", ct);
        return r?.Value ?? new();
    }

    public async Task<UserGroup> CreateGroupAsync(string name, string description, string location = "\\", CancellationToken ct = default)
    {
        _log.Information("CreateGroup requested.");
        return await PostJson<UserGroup>($"{Base}/API/UserGroups",
                   new { groupName = name, description, location }, ct)
               ?? throw new InvalidOperationException("Error al crear grupo.");
    }

    public async Task DeleteGroupAsync(int groupId, CancellationToken ct = default)
    {
        _log.Warning("DeleteGroup. GroupId={Id}", groupId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/UserGroups/{groupId}", timeoutToken), ct), ct);
    }

    public async Task RemoveGroupMemberAsync(int groupId, string username, CancellationToken ct = default)
    {
        _log.Information("RemoveGroupMember requested. GroupId={Group}", groupId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/UserGroups/{groupId}/Members/{username}", timeoutToken), ct), ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // APPLICATIONS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<CyberArkApplication>> GetApplicationsAsync(string? appIdFilter = null, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(appIdFilter) ? "" : $"?AppID={Uri.EscapeDataString(appIdFilter)}";
        var r = await GetJson<ApplicationsResponse>($"{Base}/WebServices/PIMServices.svc/Applications{q}", ct);
        return r?.Application ?? new();
    }

    public async Task CreateApplicationAsync(CyberArkApplication app, CancellationToken ct = default)
    {
        _log.Information("CreateApplication requested.");
        await EnsureOk(await PostRaw($"{Base}/WebServices/PIMServices.svc/Applications",
            new { application = app }, ct), ct);
    }

    public async Task DeleteApplicationAsync(string appId, CancellationToken ct = default)
    {
        _log.Warning("DeleteApplication. AppId={Id}", appId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/WebServices/PIMServices.svc/Applications/{appId}", timeoutToken), ct), ct);
    }

    public async Task<List<AppAuthMethod>> GetAppAuthMethodsAsync(string appId, CancellationToken ct = default)
    {
        var r = await GetJson<AppAuthMethodsResponse>($"{Base}/WebServices/PIMServices.svc/Applications/{appId}/Authentications", ct);
        return r?.authentication ?? new();
    }

    public async Task AddAppAuthMethodAsync(string appId, AppAuthMethod method, CancellationToken ct = default)
        => await EnsureOk(await PostRaw($"{Base}/WebServices/PIMServices.svc/Applications/{appId}/Authentications",
            new { authentication = method }, ct), ct);

    public async Task DeleteAppAuthMethodAsync(string appId, int methodId, CancellationToken ct = default)
        => await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/WebServices/PIMServices.svc/Applications/{appId}/Authentications/{methodId}", timeoutToken), ct), ct);

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PSM SESSIONS & RECORDINGS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<PsmSession>> GetActiveSessionsAsync(CancellationToken ct = default)
    {
        var r = await GetJson<PsmSessionsResponse>($"{Base}/API/LiveSessions?limit=200", ct);
        return r?.Activities ?? new();
    }

    public async Task<List<PsmSession>> GetSessionsHistoryAsync(string? safe = null, string? user = null,
        DateTime? from = null, DateTime? to = null, CancellationToken ct = default)
    {
        var q = new List<string> { "limit=200" };
        if (!string.IsNullOrWhiteSpace(safe)) q.Add($"Safe={Uri.EscapeDataString(safe)}");
        if (!string.IsNullOrWhiteSpace(user)) q.Add($"User={Uri.EscapeDataString(user)}");
        if (from.HasValue) q.Add($"FromTime={new DateTimeOffset(from.Value).ToUnixTimeSeconds()}");
        if (to.HasValue)   q.Add($"ToTime={new DateTimeOffset(to.Value).ToUnixTimeSeconds()}");
        var r = await GetJson<PsmSessionsResponse>($"{Base}/API/Sessions?{string.Join("&", q)}", ct);
        return r?.Activities ?? new();
    }

    public async Task TerminateSessionAsync(string sessionId, CancellationToken ct = default)
    {
        _log.Warning("TerminateSession. SessionId={Id}", sessionId);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/LiveSessions/{sessionId}/Terminate", timeoutToken), ct), ct);
    }

    public async Task<List<PsmRecording>> GetRecordingsAsync(string? safe = null, CancellationToken ct = default)
    {
        var q = string.IsNullOrWhiteSpace(safe) ? "?limit=200" : $"?Safe={Uri.EscapeDataString(safe)}&limit=200";
        var r = await GetJson<PsmRecordingsResponse>($"{Base}/API/Recordings{q}", ct);
        return r?.Recordings ?? new();
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // SYSTEM HEALTH
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<SystemHealthComponent>> GetSystemHealthAsync(CancellationToken ct = default)
    {
        var r = await GetJson<SystemHealthResponse>($"{Base}/API/ComponentsMonitoringDetails", ct);
        return r?.Components ?? new();
    }

    public async Task<SystemHealthComponent?> GetComponentHealthAsync(string componentId, CancellationToken ct = default)
        => await GetJson<SystemHealthComponent>($"{Base}/API/ComponentsMonitoringDetails/{componentId}", ct);

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // DISCOVERED ACCOUNTS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<DiscoveredAccount>> GetDiscoveredAccountsAsync(string? type = null, string? keyword = null, CancellationToken ct = default)
    {
        var q = new List<string> { "limit=100" };
        if (!string.IsNullOrWhiteSpace(type))    q.Add($"type={type}");
        if (!string.IsNullOrWhiteSpace(keyword)) q.Add($"keyword={Uri.EscapeDataString(keyword)}");
        var results = new List<DiscoveredAccount>();
        string? url = $"{Base}/API/DiscoveredAccounts?{string.Join("&", q)}";
        while (!string.IsNullOrEmpty(url))
        {
            var r = await GetJson<DiscoveredAccountsResponse>(url, ct);
            if (r?.Value is not null) results.AddRange(r.Value);
            url = string.IsNullOrEmpty(r?.NextLink) ? null : (r!.NextLink!.StartsWith("http") ? r.NextLink : Base + r.NextLink);
        }
        return results;
    }

    public async Task<Account> OnboardDiscoveredAccountAsync(string id, string safeName, string platformId, CancellationToken ct = default)
    {
        _log.Information("OnboardDiscoveredAccount. Id={Id} Safe={Safe} Platform={Platform}", id, safeName, platformId);
        return await PostJson<Account>($"{Base}/API/DiscoveredAccounts/{id}",
                   new { SafeName = safeName, PlatformID = platformId }, ct)
               ?? throw new InvalidOperationException("Error al incorporar cuenta.");
    }

    public async Task DeleteDiscoveredAccountAsync(string id, CancellationToken ct = default)
    {
        _log.Warning("DeleteDiscoveredAccount. Id={Id}", id);
        await EnsureOk(await ExecuteWithRetry(timeoutToken => _http.DeleteAsync($"{Base}/API/DiscoveredAccounts/{id}", timeoutToken), ct), ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // ACCESS REQUESTS (Dual Control)
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<List<AccessRequest>> GetMyRequestsAsync(CancellationToken ct = default)
    {
        var r = await GetJson<AccessRequestsResponse>($"{Base}/API/MyRequests?onlyOpen=false", ct);
        return r?.GetSafesWithConfirmationResponse ?? new();
    }

    public async Task ConfirmRequestAsync(string accountId, string requestId, string reason, CancellationToken ct = default)
    {
        _log.Information("ConfirmRequest. AccountId={Account} RequestId={Req}", accountId, requestId);
        await EnsureOk(await PostRaw($"{Base}/API/Accounts/{accountId}/ConfirmCredentialsChange",
            new { Reason = reason, RequestID = requestId }, ct), ct);
    }

    public async Task RejectRequestAsync(string accountId, string requestId, string reason, CancellationToken ct = default)
    {
        _log.Warning("RejectRequest. AccountId={Account} RequestId={Req}", accountId, requestId);
        await EnsureOk(await PostRaw($"{Base}/API/Accounts/{accountId}/DenyCredentialsChange",
            new { Reason = reason, RequestID = requestId }, ct), ct);
    }

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // BULK OPERATIONS
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    public async Task<BulkUploadResult> BulkCreateAsync(IEnumerable<AccountCreateRequest> accounts,
        IProgress<BulkUploadProgress>? progress = null, CancellationToken ct = default)
    {
        var list = accounts.ToList();
        var result = new BulkUploadResult { Total = list.Count };
        var errors = new ConcurrentBag<string>();
        var completed = 0;
        var succeeded = 0;
        var failed = 0;
        using var gate = new SemaphoreSlim(MaxBulkParallelism);
        _log.Information("BulkCreate started. Total={Count}", list.Count);

        var tasks = list.Select(async (acc, index) =>
        {
            await gate.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                await CreateAccountAsync(acc, ct);
                var current = Interlocked.Increment(ref completed);
                Interlocked.Increment(ref succeeded);
                progress?.Report(new BulkUploadProgress
                {
                    Current = current,
                    Total = list.Count,
                    SourceIndex = index,
                    AccountLabel = $"{acc.UserName}@{acc.Address}",
                    Success = true
                });
            }
            catch (Exception ex)
            {
                var current = Interlocked.Increment(ref completed);
                Interlocked.Increment(ref failed);
                errors.Add($"[{acc.UserName}@{acc.Address}]: {ex.Message}");
                _log.Error(ex, "BulkCreate item failed.");
                progress?.Report(new BulkUploadProgress
                {
                    Current = current,
                    Total = list.Count,
                    SourceIndex = index,
                    AccountLabel = $"{acc.UserName}@{acc.Address}",
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        result.Succeeded = succeeded;
        result.Failed = failed;
        result.Errors = errors.OrderBy(error => error, StringComparer.OrdinalIgnoreCase).ToList();

        _log.Information("BulkCreate complete. Success={Ok} Failed={Fail}", result.Succeeded, result.Failed);
        return result;
    }

    public virtual async Task<LinkedAccountBatchResult> LinkSharedAccountByAddressAsync(
        IEnumerable<string> addresses,
        string linkedAccountName,
        LinkedAccountType linkType,
        IProgress<LinkedAccountProgress>? progress = null,
        CancellationToken ct = default)
    {
        var normalizedAddresses = addresses
            .Where(address => !string.IsNullOrWhiteSpace(address))
            .Select(address => address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedAddresses.Count == 0)
        {
            throw new InvalidOperationException("No se han indicado servidores para enlazar cuentas.");
        }

        if (string.IsNullOrWhiteSpace(linkedAccountName))
        {
            throw new InvalidOperationException("Debes indicar el nombre de la cuenta de logon o reconciliaci\u00f3n.");
        }

        var targetName = linkedAccountName.Trim();
        var result = new LinkedAccountBatchResult { TotalServers = normalizedAddresses.Count };
        var errors = new ConcurrentBag<string>();
        var completedServers = 0;
        var linkedAccounts = 0;
        var succeededServers = 0;
        var failedServers = 0;
        using var gate = new SemaphoreSlim(MaxLinkParallelism);

        _log.Information("LinkSharedAccountByAddress started. Servers={Count} LinkType={Type}",
            normalizedAddresses.Count, linkType);

        var tasks = normalizedAddresses.Select(async address =>
        {
            await gate.WaitAsync(ct);
            try
            {
                var linkedCount = 0;
                var currentErrors = new List<string>();
                List<Account> serverAccounts;
                Account? linkedAccount;

                try
                {
                    ct.ThrowIfCancellationRequested();
                    var accounts = await GetAccountsAsync(search: address, ct: ct);
                    serverAccounts = accounts
                        .Where(account => account.Address.Equals(address, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    linkedAccount = serverAccounts.FirstOrDefault(account => IsLinkedAccountMatch(account, targetName));
                }
                catch (Exception ex)
                {
                    serverAccounts = new List<Account>();
                    linkedAccount = null;
                    currentErrors.Add($"[{address}] {ex.Message}");
                }

                if (currentErrors.Count == 0 && serverAccounts.Count == 0)
                {
                    currentErrors.Add($"[{address}] No se encontraron cuentas con esa direcci\u00f3n.");
                }
                else if (currentErrors.Count == 0 && linkedAccount is null)
                {
                    currentErrors.Add($"[{address}] No existe ninguna cuenta '{targetName}' para enlazar.");
                }
                else if (currentErrors.Count == 0)
                {
                    var linkedAccountId = linkedAccount!.Id;
                    var targets = serverAccounts
                        .Where(account => !account.Id.Equals(linkedAccountId, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (targets.Count == 0)
                    {
                        currentErrors.Add($"[{address}] No hay cuentas destino distintas de la cuenta enlazada.");
                    }
                    else
                    {
                        foreach (var account in targets)
                        {
                            try
                            {
                                await LinkAccountAsync(account.Id, linkedAccountId, (int)linkType, ct);
                                linkedCount++;
                                Interlocked.Increment(ref linkedAccounts);
                            }
                            catch (Exception ex)
                            {
                                currentErrors.Add($"[{address}] {account.UserName}: {ex.Message}");
                            }
                        }
                    }
                }

                if (currentErrors.Count == 0)
                {
                    Interlocked.Increment(ref succeededServers);
                }
                else
                {
                    Interlocked.Increment(ref failedServers);
                    foreach (var error in currentErrors)
                    {
                        errors.Add(error);
                    }
                }

                var currentServer = Interlocked.Increment(ref completedServers);
                progress?.Report(new LinkedAccountProgress
                {
                    CurrentServer = currentServer,
                    TotalServers = normalizedAddresses.Count,
                    Address = address,
                    LinkedAccounts = linkedCount,
                    Success = currentErrors.Count == 0,
                    Message = currentErrors.Count == 0
                        ? $"Cuenta {linkType} enlazada en {address}."
                        : $"Se han producido errores al enlazar {address}.",
                    ErrorMessage = currentErrors.Count == 0 ? null : string.Join(" | ", currentErrors)
                });
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks);

        result.SucceededServers = succeededServers;
        result.FailedServers = failedServers;
        result.LinkedAccounts = linkedAccounts;
        result.Errors = errors.OrderBy(error => error, StringComparer.OrdinalIgnoreCase).ToList();

        _log.Information("LinkSharedAccountByAddress complete. SuccessServers={Ok} FailedServers={Fail} LinkedAccounts={Linked}",
            result.SucceededServers, result.FailedServers, result.LinkedAccounts);

        return result;
    }

    private static bool IsLinkedAccountMatch(Account account, string targetName)
        => account.UserName.Equals(targetName, StringComparison.OrdinalIgnoreCase)
           || account.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase);

    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
    // PRIVATE HELPERS â€” HTTP Layer
    // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•

    private async Task<HttpResponseMessage> ExecuteWithRetry(
        Func<CancellationToken, Task<HttpResponseMessage>> action,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(DefaultRequestTimeout);
        return await RetryPolicy.ExecuteAsync(timeoutToken => action(timeoutToken), cts.Token);
    }

    private async Task<HttpResponseMessage> SendWithRetry(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken ct)
    {
        return await ExecuteWithRetry(async timeoutToken =>
        {
            using var request = requestFactory();
            return await _http.SendAsync(request, timeoutToken);
        }, ct);
    }

    private async Task<T?> GetJson<T>(string url, CancellationToken ct)
    {
        var r = await ExecuteWithRetry(timeoutToken => _http.GetAsync(url, timeoutToken), ct);
        await EnsureOk(r, ct);
        return JsonSerializer.Deserialize<T>(await r.Content.ReadAsStringAsync(ct), J);
    }

    private async Task<T?> PostJson<T>(string url, object body, CancellationToken ct)
    {
        var r = await PostRaw(url, body, ct);
        await EnsureOk(r, ct);
        return JsonSerializer.Deserialize<T>(await r.Content.ReadAsStringAsync(ct), J);
    }

    private async Task<HttpResponseMessage> PostRaw(string url, object body, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(body, J);
        return await ExecuteWithRetry(
            timeoutToken => _http.PostAsync(url, new StringContent(payload, Encoding.UTF8, "application/json"), timeoutToken),
            ct);
    }

    private static async Task EnsureOk(HttpResponseMessage r, CancellationToken ct = default)
    {
        if (r.IsSuccessStatusCode) return;

        var body = await r.Content.ReadAsStringAsync(ct);
        var code = (int)r.StatusCode;
        var message = code switch
        {
            401 => "No autorizado: token expirado o invalido.",
            403 => "Acceso denegado: permisos insuficientes.",
            404 => "Recurso no encontrado.",
            409 => "Conflicto: el recurso ya existe.",
            _ => ParseApiError(body)
        };

        _log.Error("HTTP error {Code} during CyberArk API request.", code);
        throw new InvalidOperationException($"[{code}] {message}");
    }

    private static string ParseApiError(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            foreach (var propertyName in new[] { "ErrorMessage", "Details", "message", "error" })
            {
                if (document.RootElement.TryGetProperty(propertyName, out var value))
                {
                    var candidate = value.GetString();
                    if (IsSafeServerMessage(candidate))
                    {
                        return candidate!;
                    }

                    break;
                }
            }
        }
        catch
        {
        }

        return "Error devuelto por el servidor.";
    }

    private static bool IsSafeServerMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var normalized = message.Trim().ReplaceLineEndings(" ");
        if (normalized.Length > 160)
        {
            return false;
        }

        return normalized.All(ch => !char.IsControl(ch));
    }
    // â”€â”€ Nested response DTOs â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private class AccountActivitiesResponse
    {
        [JsonPropertyName("Activities")] public List<AccountActivityLog>? Activities { get; set; }
    }
    private class AppAuthMethodsResponse
    {
        [JsonPropertyName("authentication")] public List<AppAuthMethod>? authentication { get; set; }
    }
}

