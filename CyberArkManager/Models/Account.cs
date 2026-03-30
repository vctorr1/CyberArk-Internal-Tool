using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

/// <summary>
/// Represents a CyberArk privileged account retrieved from the PAM API.
/// </summary>
public class Account
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("platformId")]
    public string PlatformId { get; set; } = string.Empty;

    [JsonPropertyName("safeName")]
    public string SafeName { get; set; } = string.Empty;

    [JsonPropertyName("secretType")]
    public string SecretType { get; set; } = string.Empty;

    [JsonPropertyName("createdTime")]
    public long CreatedTime { get; set; }

    [JsonPropertyName("categoryModificationTime")]
    public long CategoryModificationTime { get; set; }

    [JsonPropertyName("secretManagement")]
    public SecretManagement? SecretManagement { get; set; }

    [JsonPropertyName("remoteMachinesAccess")]
    public RemoteMachinesAccess? RemoteMachinesAccess { get; set; }

    // Derived display properties
    public string CreatedTimeDisplay =>
        CreatedTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(CreatedTime).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
            : "—";

    public string LastModifiedDisplay =>
        CategoryModificationTime > 0
            ? DateTimeOffset.FromUnixTimeSeconds(CategoryModificationTime).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
            : "—";

    public string PasswordStatusDisplay =>
        SecretManagement?.Status ?? "Unknown";
}

public class SecretManagement
{
    [JsonPropertyName("automaticManagementEnabled")]
    public bool AutomaticManagementEnabled { get; set; }

    [JsonPropertyName("manualManagementReason")]
    public string? ManualManagementReason { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("lastModifiedTime")]
    public long LastModifiedTime { get; set; }

    [JsonPropertyName("lastReconciledTime")]
    public long LastReconciledTime { get; set; }

    [JsonPropertyName("lastVerifiedTime")]
    public long LastVerifiedTime { get; set; }
}

public class RemoteMachinesAccess
{
    [JsonPropertyName("remoteMachines")]
    public string? RemoteMachines { get; set; }

    [JsonPropertyName("accessRestrictedToRemoteMachines")]
    public bool AccessRestrictedToRemoteMachines { get; set; }
}

/// <summary>
/// Paginated response wrapper from GET /Accounts
/// </summary>
public class AccountsResponse
{
    [JsonPropertyName("value")]
    public List<Account> Value { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("nextLink")]
    public string? NextLink { get; set; }
}

/// <summary>
/// Payload for creating or updating an account.
/// </summary>
public class AccountCreateRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("userName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("platformId")]
    public string PlatformId { get; set; } = string.Empty;

    [JsonPropertyName("safeName")]
    public string SafeName { get; set; } = string.Empty;

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("secretType")]
    public string SecretType { get; set; } = "password";

    [JsonPropertyName("secretManagement")]
    public SecretManagementRequest? SecretManagement { get; set; }
}

public class SecretManagementRequest
{
    [JsonPropertyName("automaticManagementEnabled")]
    public bool AutomaticManagementEnabled { get; set; } = true;

    [JsonPropertyName("manualManagementReason")]
    public string? ManualManagementReason { get; set; }
}
