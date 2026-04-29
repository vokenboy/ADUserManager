namespace ActiveManager.Services.Models;

public class TerminationRecord
{
    public int Id { get; set; }
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public DateTime TerminatedAt { get; set; } = DateTime.Now;
    public string PerformedBy { get; set; } = Environment.UserName;
    public string? TerminationReason { get; set; }

    // Actions performed
    public bool AccountDisabled { get; set; }
    public bool MovedToDisabledOU { get; set; }
    public string? TargetOU { get; set; }
    public string? OriginalOU { get; set; }
    public bool PasswordChanged { get; set; }
    public bool RemovedFromGroups { get; set; }
    public bool AccountExpirationSet { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public bool DataExported { get; set; }
    public string? ExportPath { get; set; }

    // Rollback tracking
    public bool RolledBack { get; set; }
    public DateTime? RolledBackAt { get; set; }
    public string? RolledBackBy { get; set; }
    public bool DeletedFromDirectory { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    // Memento pattern: snapshot of user state before termination
    public UserMemento? Memento { get; set; }

    // Group memberships before removal (stored as JSON)
    // Kept for backward compatibility and database serialization
    public List<GroupMembershipRecord> GroupMemberships { get; set; } = new();

    // Step results
    public List<TerminationStepResult> StepResults { get; set; } = new();

    /// <summary>
    /// ID of the ad_user_backups row taken before termination steps ran.
    /// Null if the DB was unavailable or backup was not taken.
    /// </summary>
    public long? PreTerminationBackupId { get; set; }

    /// <summary>
    /// ID of the ad_user_backups row taken after all termination steps completed (optional).
    /// </summary>
    public long? PostTerminationBackupId { get; set; }
}

public class GroupMembershipRecord
{
    public string GroupName { get; set; } = string.Empty;
    public string GroupDN { get; set; } = string.Empty;
}

public class TerminationStepResult
{
    public string StepName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.Now;
}
