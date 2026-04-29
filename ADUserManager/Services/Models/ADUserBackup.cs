namespace ActiveManager.Services.Models;

/// <summary>
/// The type of operation that triggered this backup.
/// </summary>
public enum BackupType
{
    Termination,
    Manual,
    Scheduled,
    Auto
}

/// <summary>
/// When in a workflow this backup was taken.
/// PreTermination = full state before any changes.
/// PostTermination = state after all termination steps completed.
/// Snapshot = ad-hoc point-in-time capture.
/// Rollback = state captured after a rollback was performed.
/// </summary>
public enum OperationType
{
    PreTermination,
    PostTermination,
    Snapshot,
    Rollback
}

/// <summary>
/// A full point-in-time snapshot of an AD user's state stored in the database.
/// Supports multi-tenant deployments: each record is scoped to a company.
/// </summary>
public class ADUserBackup
{
    public long Id { get; set; }
    public int CompanyId { get; set; }

    // ── Core identity ────────────────────────────────────────────────────────
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    // ── Organisational ───────────────────────────────────────────────────────
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string OrganizationalUnit { get; set; } = string.Empty;

    // ── Account status ───────────────────────────────────────────────────────
    public bool IsEnabled { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public DateTime? LastLogon { get; set; }
    public DateTime? AccountExpiration { get; set; }

    // ── Backup metadata ──────────────────────────────────────────────────────
    public BackupType BackupType { get; set; } = BackupType.Termination;
    public OperationType OperationType { get; set; } = OperationType.PreTermination;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string CreatedBy { get; set; } = Environment.UserName;

    /// <summary>Link to the termination_log row that triggered this backup (if any).</summary>
    public int? TerminationRecordId { get; set; }

    // ── Versioning ───────────────────────────────────────────────────────────
    /// <summary>Monotonically increasing per (company_id, sam_account_name).</summary>
    public int VersionNumber { get; set; } = 1;

    /// <summary>True only for the most-recent backup for this user in this company.</summary>
    public bool IsLatest { get; set; } = true;

    public string? Notes { get; set; }

    // ── Related data ─────────────────────────────────────────────────────────
    public List<ADUserBackupGroup> Groups { get; set; } = new();

    /// <summary>
    /// Populated by list queries that include a COUNT subquery.
    /// Not set by GetBackupByIdAsync (which loads the real Groups list instead).
    /// </summary>
    public int GroupCountSnapshot { get; set; }
}

/// <summary>
/// A single group membership recorded as part of an AD user backup.
/// </summary>
public class ADUserBackupGroup
{
    public long Id { get; set; }
    public long BackupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string GroupDN { get; set; } = string.Empty;
}
