using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class Platform : ObservableModel
{
    private string _id = string.Empty;
    private string _name = string.Empty;
    private string _systemType = string.Empty;
    private bool _active;
    private string _description = string.Empty;
    private string? _platformBaseId;
    private string _platformType = string.Empty;
    private string? _allowedSafes;

    [JsonPropertyName("id")]
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    [JsonPropertyName("systemType")]
    public string SystemType
    {
        get => _systemType;
        set => SetProperty(ref _systemType, value);
    }

    [JsonPropertyName("active")]
    public bool Active
    {
        get => _active;
        set
        {
            if (SetProperty(ref _active, value))
            {
                OnPropertyChanged(nameof(ActiveText));
            }
        }
    }

    [JsonPropertyName("description")]
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    [JsonPropertyName("platformBaseID")]
    public string? PlatformBaseID
    {
        get => _platformBaseId;
        set => SetProperty(ref _platformBaseId, value);
    }

    [JsonPropertyName("platformType")]
    public string PlatformType
    {
        get => _platformType;
        set => SetProperty(ref _platformType, value);
    }

    [JsonPropertyName("allowedSafes")]
    public string? AllowedSafes
    {
        get => _allowedSafes;
        set => SetProperty(ref _allowedSafes, value);
    }

    public string ActiveText => Active ? "Activa" : "Inactiva";
}

public class PlatformsResponse
{
    [JsonPropertyName("Platforms")] public List<PlatformWrapper> Platforms { get; set; } = new();
    [JsonPropertyName("Total")] public int Total { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
}

public class PlatformWrapper
{
    [JsonPropertyName("general")] public Platform? General { get; set; }
    [JsonPropertyName("properties")] public PlatformProperties? Properties { get; set; }
}

public class PlatformProperties
{
    [JsonPropertyName("required")] public List<PlatformProperty>? Required { get; set; }
    [JsonPropertyName("optional")] public List<PlatformProperty>? Optional { get; set; }
}

public class PlatformProperty
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("displayName")] public string DisplayName { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
}
