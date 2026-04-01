using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class CyberArkApplication
{
    [JsonPropertyName("AppID")]         public string AppID       { get; set; } = string.Empty;
    [JsonPropertyName("Description")]   public string Description { get; set; } = string.Empty;
    [JsonPropertyName("Location")]      public string Location    { get; set; } = string.Empty;
    [JsonPropertyName("AccessPermittedFrom")] public int AccessPermittedFrom { get; set; }
    [JsonPropertyName("AccessPermittedTo")]   public int AccessPermittedTo   { get; set; }
    [JsonPropertyName("ExpirationDate")]      public string? ExpirationDate  { get; set; }
    [JsonPropertyName("Disabled")]            public bool Disabled           { get; set; }
    [JsonPropertyName("BusinessOwnerFName")]  public string? BusinessOwnerFName { get; set; }
    [JsonPropertyName("BusinessOwnerLName")]  public string? BusinessOwnerLName { get; set; }
    [JsonPropertyName("BusinessOwnerEmail")]  public string? BusinessOwnerEmail { get; set; }
    public string StatusText => Disabled ? "Deshabilitada" : "Activa";
    public string OwnerName  => $"{BusinessOwnerFName} {BusinessOwnerLName}".Trim();
}

public class ApplicationsResponse
{
    [JsonPropertyName("application")] public List<CyberArkApplication>? Application { get; set; }
}

public class AppAuthMethod
{
    [JsonPropertyName("authType")]   public string AuthType { get; set; } = string.Empty;
    [JsonPropertyName("authValue")]  public string AuthValue { get; set; } = string.Empty;
    [JsonPropertyName("IsFolder")]   public bool? IsFolder  { get; set; }
    [JsonPropertyName("Comment")]    public string? Comment { get; set; }
}

// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
// DISCOVERED ACCOUNTS
// â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
