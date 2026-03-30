using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

/// <summary>
/// Represents a CyberArk Safe.
/// </summary>
public class Safe
{
    [JsonPropertyName("safeUrlId")]
    public string SafeUrlId { get; set; } = string.Empty;

    [JsonPropertyName("safeName")]
    public string SafeName { get; set; } = string.Empty;

    [JsonPropertyName("safeNumber")]
    public int SafeNumber { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("creator")]
    public SafeCreator? Creator { get; set; }

    [JsonPropertyName("olacEnabled")]
    public bool OlacEnabled { get; set; }

    [JsonPropertyName("managingCPM")]
    public string ManagingCPM { get; set; } = string.Empty;

    [JsonPropertyName("numberOfVersionsRetention")]
    public int NumberOfVersionsRetention { get; set; }

    [JsonPropertyName("numberOfDaysRetention")]
    public int NumberOfDaysRetention { get; set; }

    [JsonPropertyName("autoPurgeEnabled")]
    public bool AutoPurgeEnabled { get; set; }

    [JsonPropertyName("creationTime")]
    public long CreationTime { get; set; }

    [JsonPropertyName("lastModificationTime")]
    public long LastModificationTime { get; set; }

    [JsonPropertyName("isExpiredMember")]
    public bool IsExpiredMember { get; set; }

    public override string ToString() => SafeName;
}

public class SafeCreator
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

public class SafesResponse
{
    [JsonPropertyName("value")]
    public List<Safe> Value { get; set; } = new();

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
