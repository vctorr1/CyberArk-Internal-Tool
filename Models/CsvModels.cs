namespace CyberArkManager.Models;

public class CsvAccountRow : ObservableModel
{
    private int _rowNumber;
    private string _safeName = string.Empty;
    private string _platformId = string.Empty;
    private string _address = string.Empty;
    private string _userName = string.Empty;
    private string? _password;
    private string? _description;
    private bool _autoManagement = true;
    private string? _manualReason;
    private bool _useSudoOnReconcile;
    private string? _groupName;
    private string? _groupPlatformId;
    private string? _remoteMachines;
    private string _statusText = "-";
    private string _statusColor = "#888888";

    public int RowNumber
    {
        get => _rowNumber;
        set => SetProperty(ref _rowNumber, value);
    }

    public string SafeName
    {
        get => _safeName;
        set => SetProperty(ref _safeName, value);
    }

    public string PlatformId
    {
        get => _platformId;
        set => SetProperty(ref _platformId, value);
    }

    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    public string UserName
    {
        get => _userName;
        set => SetProperty(ref _userName, value);
    }

    public string? Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string? Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool AutoManagement
    {
        get => _autoManagement;
        set => SetProperty(ref _autoManagement, value);
    }

    public string? ManualReason
    {
        get => _manualReason;
        set => SetProperty(ref _manualReason, value);
    }

    public bool UseSudoOnReconcile
    {
        get => _useSudoOnReconcile;
        set => SetProperty(ref _useSudoOnReconcile, value);
    }

    public string? GroupName
    {
        get => _groupName;
        set => SetProperty(ref _groupName, value);
    }

    public string? GroupPlatformId
    {
        get => _groupPlatformId;
        set => SetProperty(ref _groupPlatformId, value);
    }

    public string? RemoteMachines
    {
        get => _remoteMachines;
        set => SetProperty(ref _remoteMachines, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public string StatusColor
    {
        get => _statusColor;
        set => SetProperty(ref _statusColor, value);
    }
}

public class ProcessedCsvRecord
{
    public string Label { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public int RowCount { get; set; }
    public bool IsEncrypted { get; set; }

    public string CreatedAtDisplay => CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss");
    public string StorageDisplay => IsEncrypted ? "Encrypted local snapshot" : "Legacy plain CSV";
}

public class BulkUploadResult
{
    public int Total { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => Failed > 0;
}

public class BulkUploadProgress
{
    public int Current { get; set; }
    public int Total { get; set; }
    public int SourceIndex { get; set; }
    public string AccountLabel { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public double Percent => Total == 0 ? 0 : (double)Current / Total * 100;
}

public enum LinkedAccountType
{
    Logon = 1,
    Reconcile = 3
}

public sealed class LinkedAccountTypeOption
{
    public required LinkedAccountType Type { get; init; }
    public required string DisplayName { get; init; }
    public int ExtraPasswordIndex => (int)Type;

    public static IReadOnlyList<LinkedAccountTypeOption> CreateDefaultOptions()
        => new[]
        {
            new LinkedAccountTypeOption
            {
                Type = LinkedAccountType.Logon,
                DisplayName = "Logon account"
            },
            new LinkedAccountTypeOption
            {
                Type = LinkedAccountType.Reconcile,
                DisplayName = "Reconciliation account"
            }
        };
}

public sealed class LinkedAccountProgress
{
    public int CurrentServer { get; set; }
    public int TotalServers { get; set; }
    public string Address { get; set; } = string.Empty;
    public int LinkedAccounts { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public double Percent => TotalServers == 0 ? 0 : (double)CurrentServer / TotalServers * 100;
}

public sealed class LinkedAccountBatchResult
{
    public int TotalServers { get; set; }
    public int SucceededServers { get; set; }
    public int FailedServers { get; set; }
    public int LinkedAccounts { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool HasErrors => FailedServers > 0;
}
