namespace ActiveManager.Services.Models;

public class DashboardSummary
{
    public SystemHealthStatus Health { get; set; } = new();
    public UserRiskMetrics UserMetrics { get; set; } = new();
    public List<RecentAdminAction> RecentActions { get; set; } = new();
    public List<DashboardAlert> Alerts { get; set; } = new();
    public int RecentBackupCount { get; set; }
    public int RecentRestoreCount { get; set; }
    public int LocalMementoCount { get; set; }
    public DateTime RefreshedAt { get; set; } = DateTime.Now;
}

public class SystemHealthStatus
{
    public bool AdConnected { get; set; }
    public bool DbAvailable { get; set; }
    public string DomainName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public int ErrorCount { get; set; }
}

public class UserRiskMetrics
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int DisabledUsers { get; set; }
    public int LockedUsers { get; set; }
    public int InactiveUsers { get; set; }
    public int InactivityThresholdDays { get; set; }
}

public class RecentAdminAction
{
    public string ActionType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Details { get; set; } = string.Empty;
}

public class AdminActionRecord
{
    public int Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = Environment.UserName;
    public DateTime ActionAt { get; set; } = DateTime.Now;
    public string Details { get; set; } = string.Empty;
}

public class DashboardAlert
{
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
}
