namespace ActiveManager.Services.Models;

/// <summary>
/// Memento pattern: Captures an immutable snapshot of a user's AD state
/// before any destructive operations are performed.
/// This snapshot can later be used to restore the user to their original state.
/// Supports both On-Premises AD and Azure AD.
/// </summary>
public class UserMemento
{
    public string SamAccountName { get; }
    public string DisplayName { get; }
    public string DistinguishedName { get; }
    public string? OrganizationalUnit { get; } // Nullable for Azure AD
    public bool IsEnabled { get; }
    public bool IsLockedOut { get; }
    public DateTime? AccountExpiration { get; }
    public List<GroupMembershipRecord> GroupMemberships { get; }
    public DateTime CapturedAt { get; }
    public string CapturedBy { get; }

    // Azure AD specific properties
    public DirectoryType DirectoryType { get; }
    public string? ObjectId { get; } // For Azure AD

    public UserMemento(
        string samAccountName,
        string displayName,
        string distinguishedName,
        string? organizationalUnit,
        bool isEnabled,
        bool isLockedOut,
        DateTime? accountExpiration,
        List<GroupMembershipRecord> groupMemberships,
        DateTime capturedAt,
        string capturedBy,
        DirectoryType directoryType = DirectoryType.OnPremisesAD,
        string? objectId = null)
    {
        SamAccountName = samAccountName;
        DisplayName = displayName;
        DistinguishedName = distinguishedName;
        OrganizationalUnit = organizationalUnit;
        IsEnabled = isEnabled;
        IsLockedOut = isLockedOut;
        AccountExpiration = accountExpiration;
        // Defensive copy to ensure immutability
        GroupMemberships = new List<GroupMembershipRecord>(groupMemberships);
        CapturedAt = capturedAt;
        CapturedBy = capturedBy;
        DirectoryType = directoryType;
        ObjectId = objectId;
    }
}
