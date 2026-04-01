using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class Safe
{
    [JsonPropertyName("safeUrlId")]              public string SafeUrlId   { get; set; } = string.Empty;
    [JsonPropertyName("safeName")]               public string SafeName    { get; set; } = string.Empty;
    [JsonPropertyName("safeNumber")]             public int    SafeNumber  { get; set; }
    [JsonPropertyName("description")]            public string Description { get; set; } = string.Empty;
    [JsonPropertyName("location")]               public string Location    { get; set; } = string.Empty;
    [JsonPropertyName("managingCPM")]            public string ManagingCPM { get; set; } = string.Empty;
    [JsonPropertyName("numberOfVersionsRetention")] public int NumberOfVersionsRetention { get; set; }
    [JsonPropertyName("numberOfDaysRetention")]  public int NumberOfDaysRetention  { get; set; }
    [JsonPropertyName("autoPurgeEnabled")]       public bool AutoPurgeEnabled      { get; set; }
    [JsonPropertyName("olacEnabled")]            public bool OlacEnabled           { get; set; }
    [JsonPropertyName("creationTime")]           public long CreationTime          { get; set; }
    [JsonPropertyName("creator")]                public SafeCreator? Creator       { get; set; }
    public string CreationDisplay => CreationTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(CreationTime).LocalDateTime.ToString("dd/MM/yyyy") : "-";
    public override string ToString() => SafeName;
}

public class SafeCreator
{
    [JsonPropertyName("id")]   public string Id   { get; set; } = string.Empty;
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

public class SafesResponse
{
    [JsonPropertyName("value")]    public List<Safe> Value  { get; set; } = new();
    [JsonPropertyName("count")]    public int        Count  { get; set; }
    [JsonPropertyName("nextLink")] public string?    NextLink { get; set; }
}

public class SafeCreateRequest
{
    [JsonPropertyName("safeName")]                  public string SafeName    { get; set; } = string.Empty;
    [JsonPropertyName("description")]               public string Description { get; set; } = string.Empty;
    [JsonPropertyName("location")]                  public string Location    { get; set; } = string.Empty;
    [JsonPropertyName("managingCPM")]               public string ManagingCPM { get; set; } = string.Empty;
    [JsonPropertyName("numberOfVersionsRetention")] public int NumberOfVersionsRetention { get; set; } = 5;
    [JsonPropertyName("numberOfDaysRetention")]     public int NumberOfDaysRetention     { get; set; } = 7;
    [JsonPropertyName("autoPurgeEnabled")]          public bool AutoPurgeEnabled         { get; set; } = false;
    [JsonPropertyName("olacEnabled")]               public bool OlacEnabled              { get; set; } = false;
}

public class SafeMember
{
    [JsonPropertyName("safeUrlId")]          public string SafeUrlId   { get; set; } = string.Empty;
    [JsonPropertyName("safeName")]           public string SafeName    { get; set; } = string.Empty;
    [JsonPropertyName("safeNumber")]         public int    SafeNumber  { get; set; }
    [JsonPropertyName("memberId")]           public string MemberId    { get; set; } = string.Empty;
    [JsonPropertyName("memberName")]         public string MemberName  { get; set; } = string.Empty;
    [JsonPropertyName("memberType")]         public string MemberType  { get; set; } = string.Empty;
    [JsonPropertyName("membershipExpirationDate")] public long? MembershipExpirationDate { get; set; }
    [JsonPropertyName("isExpiredMembershipEnable")] public bool IsExpiredMembershipEnable { get; set; }
    [JsonPropertyName("isPredefinedUser")]   public bool IsPredefinedUser { get; set; }
    [JsonPropertyName("permissions")]        public SafePermissions? Permissions { get; set; }
    public string ExpirationDisplay => MembershipExpirationDate.HasValue
        ? DateTimeOffset.FromUnixTimeSeconds(MembershipExpirationDate.Value).LocalDateTime.ToString("dd/MM/yyyy") : "Sin expiración";
}

public class SafePermissions
{
    [JsonPropertyName("useAccounts")]                          public bool UseAccounts { get; set; }
    [JsonPropertyName("retrieveAccounts")]                     public bool RetrieveAccounts { get; set; }
    [JsonPropertyName("listAccounts")]                         public bool ListAccounts { get; set; }
    [JsonPropertyName("addAccounts")]                          public bool AddAccounts { get; set; }
    [JsonPropertyName("updateAccountContent")]                 public bool UpdateAccountContent { get; set; }
    [JsonPropertyName("updateAccountProperties")]              public bool UpdateAccountProperties { get; set; }
    [JsonPropertyName("initiateCPMAccountManagementOperations")] public bool InitiateCPMAccountManagementOperations { get; set; }
    [JsonPropertyName("specifyNextAccountContent")]            public bool SpecifyNextAccountContent { get; set; }
    [JsonPropertyName("renameAccounts")]                       public bool RenameAccounts { get; set; }
    [JsonPropertyName("deleteAccounts")]                       public bool DeleteAccounts { get; set; }
    [JsonPropertyName("unlockAccounts")]                       public bool UnlockAccounts { get; set; }
    [JsonPropertyName("manageSafe")]                           public bool ManageSafe { get; set; }
    [JsonPropertyName("manageSafeMembers")]                    public bool ManageSafeMembers { get; set; }
    [JsonPropertyName("backupSafe")]                           public bool BackupSafe { get; set; }
    [JsonPropertyName("viewAuditLog")]                         public bool ViewAuditLog { get; set; }
    [JsonPropertyName("viewSafeMembers")]                      public bool ViewSafeMembers { get; set; }
    [JsonPropertyName("accessWithoutConfirmation")]            public bool AccessWithoutConfirmation { get; set; }
    [JsonPropertyName("createFolders")]                        public bool CreateFolders { get; set; }
    [JsonPropertyName("deleteFolders")]                        public bool DeleteFolders { get; set; }
    [JsonPropertyName("moveAccountsAndFolders")]               public bool MoveAccountsAndFolders { get; set; }
    [JsonPropertyName("requestsAuthorizationLevel1")]          public bool RequestsAuthorizationLevel1 { get; set; }
    [JsonPropertyName("requestsAuthorizationLevel2")]          public bool RequestsAuthorizationLevel2 { get; set; }
}

public class SafeMembersResponse
{
    [JsonPropertyName("value")]    public List<SafeMember> Value  { get; set; } = new();
    [JsonPropertyName("count")]    public int Count               { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink        { get; set; }
}

// Tipos y DTOs relacionados con safes y miembros.


