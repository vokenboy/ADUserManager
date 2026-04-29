using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public class DashboardService
{
    private readonly UserService? _userService;
    private readonly DatabaseService _databaseService;
    private readonly BackupService _backupService;
    private readonly MementoCaretaker _mementoCaretaker;

    public DashboardService(
        UserService? userService,
        DatabaseService databaseService,
        BackupService backupService,
        MementoCaretaker mementoCaretaker)
    {
        _userService = userService;
        _databaseService = databaseService;
        _backupService = backupService;
        _mementoCaretaker = mementoCaretaker;
    }

    public async Task<DashboardSummary> GetSummaryAsync(
        int inactivityThresholdDays = 30,
        int recentWindowDays = 7,
        int recentActionLimit = 8)
    {
        var now = DateTime.Now;
        var recentSince = now.AddDays(-recentWindowDays);

        List<ADUserModel> users = new();
        if (_userService != null)
        {
            try
            {
                users = _userService.SearchUsers(string.Empty);
            }
            catch (Exception ex)
            {
                ErrorLogger.Log("Dashboard user metrics", ex);
            }
        }

        List<TerminationRecord> terminations = new();
        List<AdminActionRecord> adminActions = new();
        List<ADUserBackup> recentBackups = new();

        try
        {
            await _databaseService.EnsureInitializedAsync();
            if (_databaseService.IsAvailable)
            {
                terminations = await _databaseService.GetRecentTerminationRecordsAsync(recentSince, recentActionLimit * 2);
                adminActions = await _databaseService.GetRecentAdminActionsAsync(recentSince, recentActionLimit * 2);
                recentBackups = await _backupService.GetCompanyBackupsAsync(since: recentSince, limit: recentActionLimit * 4);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Dashboard database metrics", ex);
        }

        return BuildSummary(
            users,
            terminations,
            adminActions,
            recentBackups,
            ErrorLogger.LogEntries,
            _userService != null,
            _databaseService.IsAvailable,
            _userService?.DomainName ?? string.Empty,
            ResolveCompanyName(),
            inactivityThresholdDays,
            recentSince,
            now,
            _mementoCaretaker.Count,
            recentActionLimit);
    }

    internal static DashboardSummary BuildSummary(
        IReadOnlyCollection<ADUserModel> users,
        IReadOnlyCollection<TerminationRecord> terminations,
        IReadOnlyCollection<AdminActionRecord> adminActions,
        IReadOnlyCollection<ADUserBackup> recentBackups,
        IReadOnlyCollection<ErrorLogEntry> errorEntries,
        bool adConnected,
        bool dbAvailable,
        string domainName,
        string companyName,
        int inactivityThresholdDays,
        DateTime recentSince,
        DateTime now,
        int localMementoCount,
        int recentActionLimit)
    {
        var inactiveCutoff = now.AddDays(-inactivityThresholdDays);
        var inactiveUsers = users.Count(user => !user.LastLogon.HasValue || user.LastLogon.Value < inactiveCutoff);
        var disabledUsers = users.Count(user => !user.IsEnabled);
        var lockedUsers = users.Count(user => user.IsLockedOut);
        var activeUsers = users.Count(user => user.IsEnabled && !user.IsLockedOut);
        var recentRestores = terminations.Count(record => record.RolledBack && record.RolledBackAt >= recentSince);

        var actions = adminActions
            .Select(record => new RecentAdminAction
            {
                ActionType = record.ActionType,
                DisplayName = record.DisplayName,
                SamAccountName = record.SamAccountName,
                PerformedBy = record.PerformedBy,
                Timestamp = record.ActionAt,
                Details = record.Details
            })
            .Concat(terminations
                .Select(record => new RecentAdminAction
                {
                    ActionType = "Terminate user",
                    DisplayName = record.DisplayName,
                    SamAccountName = record.SamAccountName,
                    PerformedBy = record.PerformedBy,
                    Timestamp = record.TerminatedAt,
                    Details = record.TargetOU is { Length: > 0 }
                        ? $"Moved to {record.TargetOU}"
                        : "Termination recorded"
                })
                .Where(action => !HasMatchingAdminAction(action, adminActions)))
            .Concat(terminations
                .Where(record => record.RolledBack && record.RolledBackAt.HasValue)
                .Select(record => new RecentAdminAction
                {
                    ActionType = "Restore user",
                    DisplayName = record.DisplayName,
                    SamAccountName = record.SamAccountName,
                    PerformedBy = record.RolledBackBy ?? string.Empty,
                    Timestamp = record.RolledBackAt!.Value,
                    Details = "Termination rolled back"
                })
                .Where(action => !HasMatchingAdminAction(action, adminActions)))
            .OrderByDescending(action => action.Timestamp)
            .Take(recentActionLimit)
            .ToList();

        var alerts = new List<DashboardAlert>();
        if (!adConnected)
        {
            alerts.Add(new DashboardAlert { Severity = "High", Message = "Active Directory is unavailable. User actions are limited." });
        }

        if (!dbAvailable)
        {
            alerts.Add(new DashboardAlert { Severity = "High", Message = "Database is unavailable. Audit history and backup operations may be incomplete." });
        }

        if (lockedUsers > 0)
        {
            alerts.Add(new DashboardAlert { Severity = "Medium", Message = $"{lockedUsers} user account(s) are currently locked." });
        }

        if (inactiveUsers > 0)
        {
            alerts.Add(new DashboardAlert { Severity = "Medium", Message = $"{inactiveUsers} user account(s) have been inactive for more than {inactivityThresholdDays} days or never signed in." });
        }

        if (errorEntries.Count > 0)
        {
            alerts.Add(new DashboardAlert { Severity = "Medium", Message = $"{errorEntries.Count} error log entr{(errorEntries.Count == 1 ? "y" : "ies")} recorded in this app session." });
        }

        if (recentBackups.Count == 0 && dbAvailable)
        {
            alerts.Add(new DashboardAlert { Severity = "Info", Message = "No recent remote backups were found in the selected activity window." });
        }

        return new DashboardSummary
        {
            Health = new SystemHealthStatus
            {
                AdConnected = adConnected,
                DbAvailable = dbAvailable,
                DomainName = domainName,
                CompanyName = companyName,
                ErrorCount = errorEntries.Count
            },
            UserMetrics = new UserRiskMetrics
            {
                TotalUsers = users.Count,
                ActiveUsers = activeUsers,
                DisabledUsers = disabledUsers,
                LockedUsers = lockedUsers,
                InactiveUsers = inactiveUsers,
                InactivityThresholdDays = inactivityThresholdDays
            },
            RecentActions = actions,
            Alerts = alerts,
            RecentBackupCount = recentBackups.Count,
            RecentRestoreCount = recentRestores,
            LocalMementoCount = localMementoCount,
            RefreshedAt = now
        };
    }

    private static string ResolveCompanyName()
    {
        if (!string.IsNullOrWhiteSpace(AppSettings.Instance.Database.CompanyName))
        {
            return AppSettings.Instance.Database.CompanyName;
        }

        return string.IsNullOrWhiteSpace(Environment.UserDomainName)
            ? "Not configured"
            : Environment.UserDomainName;
    }

    private static bool HasMatchingAdminAction(RecentAdminAction action, IReadOnlyCollection<AdminActionRecord> adminActions)
    {
        return adminActions.Any(record =>
            string.Equals(NormalizeActionType(record.ActionType), NormalizeActionType(action.ActionType), StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.SamAccountName, action.SamAccountName, StringComparison.OrdinalIgnoreCase) &&
            Math.Abs((record.ActionAt - action.Timestamp).TotalMinutes) <= 10);
    }

    private static string NormalizeActionType(string actionType)
    {
        return actionType.Trim().ToLowerInvariant() switch
        {
            "termination" => "terminate user",
            "terminate" => "terminate user",
            "restore" => "restore user",
            _ => actionType.Trim().ToLowerInvariant()
        };
    }
}
