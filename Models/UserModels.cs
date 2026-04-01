using System.Text.Json.Serialization;

namespace CyberArkManager.Models;

public class CyberArkUser : ObservableModel
{
    private int _id;
    private string _username = string.Empty;
    private string _source = string.Empty;
    private string _userType = string.Empty;
    private bool _componentUser;
    private string? _firstName;
    private string? _lastName;
    private string? _email;
    private string? _location;
    private bool _suspended;
    private long _lastSuccessfulLoginDate;
    private List<UserGroup>? _groups;
    private List<string>? _vaultAuthorization;

    [JsonPropertyName("id")]
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    [JsonPropertyName("username")]
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    [JsonPropertyName("source")]
    public string Source
    {
        get => _source;
        set => SetProperty(ref _source, value);
    }

    [JsonPropertyName("userType")]
    public string UserType
    {
        get => _userType;
        set => SetProperty(ref _userType, value);
    }

    [JsonPropertyName("componentUser")]
    public bool ComponentUser
    {
        get => _componentUser;
        set => SetProperty(ref _componentUser, value);
    }

    [JsonPropertyName("firstName")]
    public string? FirstName
    {
        get => _firstName;
        set
        {
            if (SetProperty(ref _firstName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    [JsonPropertyName("lastName")]
    public string? LastName
    {
        get => _lastName;
        set
        {
            if (SetProperty(ref _lastName, value))
            {
                OnPropertyChanged(nameof(FullName));
            }
        }
    }

    [JsonPropertyName("email")]
    public string? Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    [JsonPropertyName("location")]
    public string? Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    [JsonPropertyName("suspended")]
    public bool Suspended
    {
        get => _suspended;
        set
        {
            if (SetProperty(ref _suspended, value))
            {
                OnPropertyChanged(nameof(StatusText));
            }
        }
    }

    [JsonPropertyName("lastSuccessfulLoginDate")]
    public long LastSuccessfulLoginDate
    {
        get => _lastSuccessfulLoginDate;
        set
        {
            if (SetProperty(ref _lastSuccessfulLoginDate, value))
            {
                OnPropertyChanged(nameof(LastLogin));
            }
        }
    }

    [JsonPropertyName("groups")]
    public List<UserGroup>? Groups
    {
        get => _groups;
        set => SetProperty(ref _groups, value);
    }

    [JsonPropertyName("vaultAuthorization")]
    public List<string>? VaultAuthorization
    {
        get => _vaultAuthorization;
        set => SetProperty(ref _vaultAuthorization, value);
    }

    public string FullName => $"{FirstName} {LastName}".Trim();
    public string StatusText => Suspended ? "Suspendido" : "Activo";
    public string LastLogin => LastSuccessfulLoginDate > 0
        ? DateTimeOffset.FromUnixTimeSeconds(LastSuccessfulLoginDate).LocalDateTime.ToString("dd/MM/yyyy HH:mm")
        : "Nunca";
}

public class UsersResponse
{
    [JsonPropertyName("Users")] public List<CyberArkUser> Users { get; set; } = new();
    [JsonPropertyName("Total")] public int Total { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
}

public class UserCreateRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("userType")] public string UserType { get; set; } = "EPVUser";
    [JsonPropertyName("initialPassword")] public string InitialPassword { get; set; } = string.Empty;
    [JsonPropertyName("authenticationMethod")] public List<string>? AuthenticationMethod { get; set; }
    [JsonPropertyName("location")] public string Location { get; set; } = "\\";
    [JsonPropertyName("firstName")] public string? FirstName { get; set; }
    [JsonPropertyName("lastName")] public string? LastName { get; set; }
    [JsonPropertyName("email")] public string? Email { get; set; }
    [JsonPropertyName("vaultAuthorization")] public List<string>? VaultAuthorization { get; set; }
}

public class UserGroup
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("groupType")] public string GroupType { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("location")] public string Location { get; set; } = string.Empty;
    [JsonPropertyName("membersCount")] public int MembersCount { get; set; }
}

public class GroupsResponse
{
    [JsonPropertyName("value")] public List<UserGroup> Value { get; set; } = new();
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("nextLink")] public string? NextLink { get; set; }
}
