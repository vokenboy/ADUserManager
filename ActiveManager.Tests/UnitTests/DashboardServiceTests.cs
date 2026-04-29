using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Tests.UnitTests;

public class DashboardServiceTests
{
    [Fact]
    public void BuildSummary_ComputesMetricsActionsAndAlerts()
    {
        var now = new DateTime(2026, 4, 12, 10, 0, 0);
        var recentSince = now.AddDays(-7);

        var users = new List<ADUserModel>
        {
            new() { SamAccountName = "active.user", DisplayName = "Active User", IsEnabled = true, IsLockedOut = false, LastLogon = now.AddDays(-1) },
            new() { SamAccountName = "locked.user", DisplayName = "Locked User", IsEnabled = true, IsLockedOut = true, LastLogon = now.AddDays(-3) },
            new() { SamAccountName = "disabled.user", DisplayName = "Disabled User", IsEnabled = false, IsLockedOut = false, LastLogon = now.AddDays(-60) },
            new() { SamAccountName = "never.user", DisplayName = "Never Logged", IsEnabled = true, IsLockedOut = false, LastLogon = null }
        };

        var terminations = new List<TerminationRecord>
        {
            new()
            {
                DisplayName = "Terminated User",
                SamAccountName = "terminated.user",
                PerformedBy = "admin1",
                TerminatedAt = now.AddDays(-2),
                TargetOU = "OU=Disabled",
                RolledBack = true,
                RolledBackAt = now.AddDays(-1),
                RolledBackBy = "admin2"
            }
        };

        var adminActions = new List<AdminActionRecord>
        {
            new()
            {
                ActionType = "Reset password",
                DisplayName = "Reset User",
                SamAccountName = "reset.user",
                PerformedBy = "helpdesk",
                ActionAt = now.AddHours(-12),
                Details = "Password reset and forced change at next sign-in"
            }
        };

        var backups = new List<ADUserBackup>
        {
            new() { SamAccountName = "terminated.user", CreatedAt = now.AddDays(-2) }
        };

        var errors = new List<ErrorLogEntry>
        {
            new(now.AddMinutes(-30), "Test", "Something failed", null)
        };

        var summary = DashboardService.BuildSummary(
            users,
            terminations,
            adminActions,
            backups,
            errors,
            adConnected: true,
            dbAvailable: true,
            domainName: "company.local",
            companyName: "company",
            inactivityThresholdDays: 30,
            recentSince: recentSince,
            now: now,
            localMementoCount: 2,
            recentActionLimit: 8);

        Assert.Equal(4, summary.UserMetrics.TotalUsers);
        Assert.Equal(2, summary.UserMetrics.ActiveUsers);
        Assert.Equal(1, summary.UserMetrics.DisabledUsers);
        Assert.Equal(1, summary.UserMetrics.LockedUsers);
        Assert.Equal(2, summary.UserMetrics.InactiveUsers);
        Assert.Equal(1, summary.RecentBackupCount);
        Assert.Equal(1, summary.RecentRestoreCount);
        Assert.Equal(2, summary.LocalMementoCount);
        Assert.Equal(3, summary.RecentActions.Count);
        Assert.Contains(summary.RecentActions, action => action.ActionType == "Reset password");
        Assert.Contains(summary.RecentActions, action => action.ActionType == "Terminate user");
        Assert.Contains(summary.RecentActions, action => action.ActionType == "Restore user");
        Assert.Contains(summary.Alerts, alert => alert.Message.Contains("locked", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Alerts, alert => alert.Message.Contains("inactive", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summary.Alerts, alert => alert.Message.Contains("error log", StringComparison.OrdinalIgnoreCase));
    }
}
