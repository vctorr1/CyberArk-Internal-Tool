using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class PsmSession
{
    [JsonPropertyName("SessionID")]          public string SessionID   { get; set; } = string.Empty;
    [JsonPropertyName("User")]               public string User        { get; set; } = string.Empty;
    [JsonPropertyName("RemoteMachine")]      public string RemoteMachine { get; set; } = string.Empty;
    [JsonPropertyName("Start")]              public long Start         { get; set; }
    [JsonPropertyName("End")]                public long End           { get; set; }
    [JsonPropertyName("Duration")]           public long Duration      { get; set; }
    [JsonPropertyName("Status")]             public string Status      { get; set; } = string.Empty;
    [JsonPropertyName("AccountID")]          public string? AccountID  { get; set; }
    [JsonPropertyName("AccountName")]        public string? AccountName { get; set; }
    [JsonPropertyName("AccountAddress")]     public string? AccountAddress { get; set; }
    [JsonPropertyName("AccountPlatformID")]  public string? AccountPlatformID { get; set; }
    [JsonPropertyName("ConnectionComponent")] public string? ConnectionComponent { get; set; }
    [JsonPropertyName("Protocol")]           public string? Protocol   { get; set; }
    [JsonPropertyName("RiskScore")]          public int? RiskScore     { get; set; }
    public string StartDisplay => Start > 0 ? DateTimeOffset.FromUnixTimeSeconds(Start).LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss") : "â€”";
    public string EndDisplay   => End > 0   ? DateTimeOffset.FromUnixTimeSeconds(End).LocalDateTime.ToString("dd/MM/yyyy HH:mm:ss")   : "Activa";
    public string DurationDisplay => Duration > 0 ? TimeSpan.FromSeconds(Duration).ToString(@"hh\:mm\:ss") : "â€”";
    public bool IsActive => Status.Equals("Active", StringComparison.OrdinalIgnoreCase);
}

public class PsmSessionsResponse
{
    [JsonPropertyName("Activities")] public List<PsmSession>? Activities { get; set; }
    [JsonPropertyName("Total")]      public int Total                     { get; set; }
    [JsonPropertyName("nextLink")]   public string? NextLink              { get; set; }
}

public class PsmRecording
{
    [JsonPropertyName("SessionID")]     public string SessionID     { get; set; } = string.Empty;
    [JsonPropertyName("SafeName")]      public string SafeName      { get; set; } = string.Empty;
    [JsonPropertyName("FolderName")]    public string FolderName    { get; set; } = string.Empty;
    [JsonPropertyName("FileName")]      public string FileName      { get; set; } = string.Empty;
    [JsonPropertyName("User")]          public string User          { get; set; } = string.Empty;
    [JsonPropertyName("RemoteMachine")] public string RemoteMachine { get; set; } = string.Empty;
    [JsonPropertyName("Start")]         public long Start           { get; set; }
    [JsonPropertyName("End")]           public long End             { get; set; }
    [JsonPropertyName("Duration")]      public long Duration        { get; set; }
    public string StartDisplay    => Start > 0    ? DateTimeOffset.FromUnixTimeSeconds(Start).LocalDateTime.ToString("dd/MM/yyyy HH:mm") : "â€”";
    public string DurationDisplay => Duration > 0 ? TimeSpan.FromSeconds(Duration).ToString(@"hh\:mm\:ss") : "â€”";
}

public class PsmRecordingsResponse
{
    [JsonPropertyName("Recordings")] public List<PsmRecording>? Recordings { get; set; }
    [JsonPropertyName("Total")]      public int Total                       { get; set; }
}
