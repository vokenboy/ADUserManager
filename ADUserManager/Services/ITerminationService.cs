using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public interface ITerminationService : IDisposable, IUserOriginator
{
    string DomainName { get; }
    string DomainPath { get; }

    void DisableUser(string distinguishedName);
    void EnableUser(string distinguishedName);
    void MoveUser(string distinguishedName, string targetOU);
    void ResetPassword(string samAccountName, string newPassword);
    void ResetPassword(string samAccountName, string newPassword, bool forcePasswordChangeAtNextLogon);
    void SetAccountExpiration(string distinguishedName, DateTime expirationDate);
    void ClearAccountExpiration(string distinguishedName);
    List<GroupMembershipRecord> GetUserGroups(string distinguishedName);
    void RemoveFromAllGroups(string distinguishedName);
    void AddToGroups(string distinguishedName, List<GroupMembershipRecord> groups);
    List<string> GetOrganizationalUnits();
    List<string> GetUserPlacementTargets();
    void ExportUserData(ADUserModel user, List<GroupMembershipRecord> groups,
        string format, string filePath, bool includeGroups, bool includePermissions);

    /// <summary>
    /// Find a user's current DN by their SamAccountName.
    /// Used during rollback to locate the user after they may have been moved.
    /// </summary>
    string? FindUserDN(string samAccountName);
}
