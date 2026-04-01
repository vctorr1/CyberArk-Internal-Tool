using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class Account
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("platformId")] public string PlatformId { get; set; } = string.Empty;
    [JsonPropertyName("safeName")] public string SafeName { get; set; } = string.Empty;
    [JsonPropertyName("secretType")] public string SecretType { get; set; } = string.Empty;
    [JsonPropertyName("createdTime")] public long CreatedTime { get; set; }
    [JsonPropertyName("categoryModificationTime")] public long CategoryModificationTime { get; set; }
    [JsonPropertyName("secretManagement")] public SecretManagement? SecretManagement { get; set; }
    [JsonPropertyName("remoteMachinesAccess")] public RemoteMachinesAccess? RemoteMachinesAccess { get; set; }

    public string CreatedDisplay => CreatedTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(CreatedTime).LocalDateTime.ToString("dd/MM/yyyy HH:mm") : "-";
    public string ModifiedDisplay => CategoryModificationTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(CategoryModificationTime).LocalDateTime.ToString("dd/MM/yyyy HH:mm") : "-";
    public string CpmStatus => SecretManagement?.Status ?? "Unknown";
    public bool AutoManaged => SecretManagement?.AutomaticManagementEnabled ?? false;
}

public class SecretManagement
{
    [JsonPropertyName("automaticManagementEnabled")] public bool AutomaticManagementEnabled { get; set; }
    [JsonPropertyName("manualManagementReason")] public string? ManualManagementReason { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("lastModifiedTime")] public long LastModifiedTime { get; set; }
    [JsonPropertyName("lastReconciledTime")] public long LastReconciledTime { get; set; }
    [JsonPropertyName("lastVerifiedTime")] public long LastVerifiedTime { get; set; }
}

public class RemoteMachinesAccess
{
    [JsonPropertyName("remoteMachines")] public string? RemoteMachines { get; set; }
    [JsonPropertyName("accessRestrictedToRemoteMachines")] public bool AccessRestrictedToRemoteMachines { get; set; }
}

public class AccountsResponse
{
    [JsonPropertyName("value")] public List<Account> Value { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
}

public class AccountCreateRequest
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("address")] public string Address { get; set; } = string.Empty;
    [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("platformId")] public string PlatformId { get; set; } = string.Empty;
    [JsonPropertyName("safeName")] public string SafeName { get; set; } = string.Empty;
    [JsonPropertyName("secret")] public string? Secret { get; set; }
    [JsonPropertyName("secretType")] public string SecretType { get; set; } = "password";
    [JsonPropertyName("secretManagement")] public SecretManagementRequest? SecretManagement { get; set; }
    [JsonPropertyName("platformAccountProperties")] public Dictionary<string, string>? PlatformAccountProperties { get; set; }
}

public class SecretManagementRequest
{
    [JsonPropertyName("automaticManagementEnabled")] public bool AutomaticManagementEnabled { get; set; } = true;
    [JsonPropertyName("manualManagementReason")] public string? ManualManagementReason { get; set; }
}

public class PatchOperation
{
    [JsonPropertyName("op")] public string Op { get; set; } = "replace";
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("value")] public object Value { get; set; } = string.Empty;
}

public class AccountActivityLog
{
    [JsonPropertyName("Action")] public string Action { get; set; } = string.Empty;
    [JsonPropertyName("Time")] public long Time { get; set; }
    [JsonPropertyName("User")] public string User { get; set; } = string.Empty;
    [JsonPropertyName("Reason")] public string? Reason { get; set; }

    public string TimeDisplay => Time > 0
        ? DateTimeOffset.FromUnixTimeSeconds(Time).LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss")
        : "-";
}
