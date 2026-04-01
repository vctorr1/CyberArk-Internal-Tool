using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class DiscoveredAccount
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("computerName")] public string ComputerName { get; set; } = string.Empty;
    [JsonPropertyName("userName")] public string UserName { get; set; } = string.Empty;
    [JsonPropertyName("accountType")] public string AccountType { get; set; } = string.Empty;
    [JsonPropertyName("discoveryDate")] public long DiscoveryDate { get; set; }
    [JsonPropertyName("lastLogonDate")] public long LastLogonDate { get; set; }
    [JsonPropertyName("osVersion")] public string? OsVersion { get; set; }
    [JsonPropertyName("platformType")] public string? PlatformType { get; set; }
    [JsonPropertyName("privileged")] public bool Privileged { get; set; }
    [JsonPropertyName("dependencies")] public int? Dependencies { get; set; }

    public string DiscoveryDisplay => DiscoveryDate > 0
        ? DateTimeOffset.FromUnixTimeSeconds(DiscoveryDate).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
        : "-";

    public string LastLogonDisplay => LastLogonDate > 0
        ? DateTimeOffset.FromUnixTimeSeconds(LastLogonDate).LocalDateTime.ToString("dd/MM/yyyy")
        : "-";
}

public class DiscoveredAccountsResponse
{
    [JsonPropertyName("value")] public List<DiscoveredAccount> Value { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
}

public class SystemHealthComponent
{
    [JsonPropertyName("ComponentID")] public string ComponentID { get; set; } = string.Empty;
    [JsonPropertyName("ComponentName")] public string ComponentName { get; set; } = string.Empty;
    [JsonPropertyName("ComponentVersion")] public string? ComponentVersion { get; set; }
    [JsonPropertyName("Instances")] public List<ComponentInstance>? Instances { get; set; }
}

public class ComponentInstance
{
    [JsonPropertyName("ComponentUserName")] public string? ComponentUserName { get; set; }
    [JsonPropertyName("IP")] public string? IP { get; set; }
    [JsonPropertyName("IsLoggedOn")] public bool IsLoggedOn { get; set; }
    [JsonPropertyName("LastLogonDate")] public long? LastLogonDate { get; set; }
    [JsonPropertyName("Connected")] public bool Connected { get; set; }

    public string StatusText => Connected ? "Connected" : "Disconnected";

    public string LastLogon => LastLogonDate.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(LastLogonDate.Value).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
        : "-";
}

public class SystemHealthResponse
{
    [JsonPropertyName("Components")] public List<SystemHealthComponent>? Components { get; set; }
}

public class AccessRequest
{
    [JsonPropertyName("RequestID")] public string RequestID { get; set; } = string.Empty;
    [JsonPropertyName("SafeName")] public string SafeName { get; set; } = string.Empty;
    [JsonPropertyName("RequestorUserName")] public string RequestorUserName { get; set; } = string.Empty;
    [JsonPropertyName("RequestorReason")] public string? RequestorReason { get; set; }
    [JsonPropertyName("Operation")] public string Operation { get; set; } = string.Empty;
    [JsonPropertyName("StatusTitle")] public string StatusTitle { get; set; } = string.Empty;
    [JsonPropertyName("ExpirationDate")] public long ExpirationDate { get; set; }
    [JsonPropertyName("AccountID")] public string AccountID { get; set; } = string.Empty;

    public string ExpirationDisplay => ExpirationDate > 0
        ? DateTimeOffset.FromUnixTimeSeconds(ExpirationDate).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
        : "-";
}

public class AccessRequestsResponse
{
    [JsonPropertyName("GetSafesWithConfirmationResponse")] public List<AccessRequest>? GetSafesWithConfirmationResponse { get; set; }
}
