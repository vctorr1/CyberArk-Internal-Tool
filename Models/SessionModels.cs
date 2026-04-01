namespace CyberArkManager.Models;

public class UserSession : ObservableModel
{
    private string _token = string.Empty;
    private string _username = string.Empty;
    private string _pvwaUrl = string.Empty;
    private string _authMode = "CyberArk";
    private bool _isLocalMode;
    private DateTime _loginTime;
    private DateTime _lastRenew;
    private DateTime _hardExpiry;

    internal string Token
    {
        get => _token;
        set
        {
            if (_token != value)
            {
                _token = value;
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string PvwaUrl
    {
        get => _pvwaUrl;
        set => SetProperty(ref _pvwaUrl, value);
    }

    public string AuthMode
    {
        get => _authMode;
        set => SetProperty(ref _authMode, value);
    }

    public bool IsLocalMode
    {
        get => _isLocalMode;
        set
        {
            if (SetProperty(ref _isLocalMode, value))
            {
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public DateTime LoginTime
    {
        get => _loginTime;
        set
        {
            if (SetProperty(ref _loginTime, value))
            {
                OnPropertyChanged(nameof(Duration));
                OnPropertyChanged(nameof(DurationDisplay));
            }
        }
    }

    public DateTime LastRenew
    {
        get => _lastRenew;
        set => SetProperty(ref _lastRenew, value);
    }

    public DateTime HardExpiry
    {
        get => _hardExpiry;
        set
        {
            if (SetProperty(ref _hardExpiry, value))
            {
                OnPropertyChanged(nameof(IsActive));
            }
        }
    }

    public bool IsActive => IsLocalMode || (!string.IsNullOrEmpty(Token) && DateTime.Now < HardExpiry);

    public TimeSpan Duration => DateTime.Now - LoginTime;

    public string DurationDisplay
    {
        get
        {
            var duration = Duration;
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}h {duration.Minutes}m"
                : $"{duration.Minutes}m {duration.Seconds}s";
        }
    }

    public void Invalidate() => Token = string.Empty;
}

public class AppConfiguration
{
    public string PvwaUrl { get; set; } = string.Empty;
    public string? LastUsername { get; set; }
    public bool RememberUsername { get; set; } = true;
    public bool AcceptAllCertificates { get; set; }
    public int HeartbeatIntervalMinutes { get; set; } = 10;
    public string AuthMethod { get; set; } = "CyberArk";
    public string Theme { get; set; } = "Dark";
}

public class CsvTemplate : ObservableModel
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private DateTime _updatedAtUtc = DateTime.UtcNow;
    private int _accountsPerServer = 5;
    private List<CsvAccountRow> _accountProfiles = CreateProfiles(5);

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public DateTime UpdatedAtUtc
    {
        get => _updatedAtUtc;
        set => SetProperty(ref _updatedAtUtc, value);
    }

    public CsvAccountRow Prototype
    {
        get => AccountProfiles.FirstOrDefault() ?? new CsvAccountRow();
        set
        {
            var profiles = value is null
                ? CreateProfiles(AccountsPerServer)
                : NormalizeProfiles(new[] { value }, AccountsPerServer);

            if (SetProperty(ref _accountProfiles, profiles, nameof(AccountProfiles)))
            {
                OnPropertyChanged();
            }
        }
    }

    public int AccountsPerServer
    {
        get => _accountsPerServer;
        set
        {
            var normalized = Math.Max(1, value);
            if (SetProperty(ref _accountsPerServer, normalized))
            {
                AccountProfiles = NormalizeProfiles(AccountProfiles, normalized);
            }
        }
    }

    public List<CsvAccountRow> AccountProfiles
    {
        get => _accountProfiles;
        set => SetProperty(ref _accountProfiles, NormalizeProfiles(value, AccountsPerServer));
    }

    public void EnsureConsistency()
    {
        if (AccountsPerServer <= 0)
        {
            AccountsPerServer = 5;
        }

        AccountProfiles = NormalizeProfiles(AccountProfiles, AccountsPerServer);
    }

    static List<CsvAccountRow> NormalizeProfiles(IEnumerable<CsvAccountRow>? source, int desiredCount)
    {
        var count = Math.Max(1, desiredCount);
        var profiles = source?
            .Take(count)
            .Select(CloneProfile)
            .ToList() ?? new List<CsvAccountRow>();

        while (profiles.Count < count)
        {
            profiles.Add(new CsvAccountRow
            {
                StatusText = "Template",
                StatusColor = "#6666AA"
            });
        }

        return profiles;
    }

    static List<CsvAccountRow> CreateProfiles(int count) => NormalizeProfiles(null, count);

    static CsvAccountRow CloneProfile(CsvAccountRow source) => new()
    {
        SafeName = source.SafeName,
        PlatformId = source.PlatformId,
        Address = source.Address,
        UserName = source.UserName,
        Description = source.Description,
        AutoManagement = source.AutoManagement,
        ManualReason = source.ManualReason,
        UseSudoOnReconcile = source.UseSudoOnReconcile,
        GroupName = source.GroupName,
        GroupPlatformId = source.GroupPlatformId,
        RemoteMachines = source.RemoteMachines,
        StatusText = source.StatusText,
        StatusColor = source.StatusColor
    };
}
