using System.DirectoryServices;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public class UserService : ActiveDirectoryBase, IDirectoryUserService
{
    public List<ADUserModel> SearchUsers(string filter)
    {
        var users = new List<ADUserModel>();
        using var entry = new DirectoryEntry(_domainPath);

        var ldapFilter = string.IsNullOrWhiteSpace(filter)
            ? "(&(objectClass=user)(objectCategory=person))"
            : $"(&(objectClass=user)(objectCategory=person)(|(cn=*{EscapeLdapFilter(filter)}*)(sAMAccountName=*{EscapeLdapFilter(filter)}*)(mail=*{EscapeLdapFilter(filter)}*)(department=*{EscapeLdapFilter(filter)}*)))";

        using var searcher = new DirectorySearcher(entry)
        {
            Filter = ldapFilter,
            SearchScope = SearchScope.Subtree,
            SizeLimit = 1000
        };

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "sAMAccountName", "displayName", "givenName", "sn", "mail",
            "department", "title", "description", "distinguishedName",
            "userAccountControl", "lockoutTime", "pwdLastSet", "lastLogon"
        });

        foreach (SearchResult result in searcher.FindAll())
        {
            users.Add(MapSearchResult(result));
        }

        return users;
    }

    public ADUserModel? GetUser(string samAccountName)
    {
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = $"(&(objectClass=user)(objectCategory=person)(sAMAccountName={EscapeLdapFilter(samAccountName)}))"
        };

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "sAMAccountName", "displayName", "givenName", "sn", "mail",
            "department", "title", "description", "distinguishedName",
            "userAccountControl", "lockoutTime", "pwdLastSet", "lastLogon"
        });

        var result = searcher.FindOne();
        return result == null ? null : MapSearchResult(result);
    }

    public ADUserModel? GetUserByIdentity(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return null;
        }

        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter =
                $"(&(objectClass=user)(objectCategory=person)(|(sAMAccountName={EscapeLdapFilter(identifier)})(userPrincipalName={EscapeLdapFilter(identifier)})(mail={EscapeLdapFilter(identifier)})))",
            SearchScope = SearchScope.Subtree
        };

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "sAMAccountName", "displayName", "givenName", "sn", "mail",
            "department", "title", "description", "distinguishedName",
            "userAccountControl", "lockoutTime", "pwdLastSet", "lastLogon",
            "userPrincipalName"
        });

        var result = searcher.FindOne();
        return result == null ? null : MapSearchResult(result);
    }

    public ADUserModel UpdateUser(UpdateUserRequest request)
    {
        var validationError = ValidateUpdateRequest(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            throw new InvalidOperationException(validationError);
        }

        using var userEntry = FindUserEntryBySamAccountName(request.OriginalSamAccountName.Trim());
        if (userEntry == null)
        {
            throw new InvalidOperationException(
                $"User '{request.OriginalSamAccountName}' was not found in AD.");
        }

        var requestedEmail = request.Email.Trim();
        var currentEmail = userEntry.Properties["mail"].Value?.ToString()
            ?? userEntry.Properties["userPrincipalName"].Value?.ToString()
            ?? string.Empty;

        if (HasEmailChanged(currentEmail, requestedEmail) && EmailExistsForAnotherUser(requestedEmail, userEntry.Guid))
        {
            throw new InvalidOperationException("That email address is already in use.");
        }

        var displayName = UserProvisioningService.BuildDisplayName(request.FirstName, request.LastName);
        var targetOu = request.TargetOU.Trim();
        var currentDistinguishedName = userEntry.Properties["distinguishedName"].Value?.ToString()
            ?? throw new InvalidOperationException("Failed to determine the user DN.");
        var currentOu = TerminationService.ParseOUFromDN(currentDistinguishedName) ?? string.Empty;

        SetRequiredProperty(userEntry, "givenName", request.FirstName);
        SetRequiredProperty(userEntry, "sn", request.LastName);
        SetRequiredProperty(userEntry, "displayName", displayName);
        SetRequiredProperty(userEntry, "mail", requestedEmail);
        SetRequiredProperty(userEntry, "userPrincipalName", requestedEmail);
        SetOptionalProperty(userEntry, "department", request.Department);
        SetOptionalProperty(userEntry, "title", request.Title);
        SetOptionalProperty(userEntry, "description", request.Description);
        SetAccountEnabled(userEntry, request.Enabled);
        userEntry.CommitChanges();

        var currentCn = userEntry.Properties["cn"].Value?.ToString() ?? string.Empty;
        var targetRdn = $"CN={UserProvisioningService.EscapeLdapRdn(displayName)}";
        var cnChanged = !string.Equals(currentCn, displayName, StringComparison.OrdinalIgnoreCase);
        var ouChanged = !string.Equals(currentOu, targetOu, StringComparison.OrdinalIgnoreCase);

        if (ouChanged)
        {
            using var targetEntry = new DirectoryEntry($"LDAP://{targetOu}");
            userEntry.MoveTo(targetEntry, targetRdn);
            userEntry.CommitChanges();
        }
        else if (cnChanged)
        {
            userEntry.Rename(targetRdn);
            userEntry.CommitChanges();
        }

        userEntry.RefreshCache(new[] { "distinguishedName", "memberOf" });
        var updatedDn = userEntry.Properties["distinguishedName"].Value?.ToString()
            ?? throw new InvalidOperationException("Failed to update the user DN.");

        var currentGroups = userEntry.Properties["memberOf"]
            .Cast<object>()
            .Select(group => group?.ToString() ?? string.Empty)
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .ToList();
        var groupChanges = CalculateGroupMembershipChanges(currentGroups, request.SelectedGroups);

        foreach (var groupDn in groupChanges.GroupsToAdd)
        {
            using var groupEntry = new DirectoryEntry($"LDAP://{groupDn}");
            groupEntry.Properties["member"].Add(updatedDn);
            groupEntry.CommitChanges();
        }

        foreach (var groupDn in groupChanges.GroupsToRemove)
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{groupDn}");
                groupEntry.Properties["member"].Remove(updatedDn);
                groupEntry.CommitChanges();
            }
            catch (Exception ex) when (
                ex.Message.Contains("primary group", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("0x80070561", StringComparison.OrdinalIgnoreCase))
            {
            }
        }

        var updatedUser = GetUser(request.OriginalSamAccountName.Trim());
        return updatedUser ?? throw new InvalidOperationException("Failed to read the updated user.");
    }

    public void DeleteUser(string samAccountName)
    {
        using var userEntry = FindUserEntryBySamAccountName(samAccountName.Trim());
        if (userEntry == null)
        {
            throw new InvalidOperationException($"User '{samAccountName}' was not found in AD.");
        }

        try
        {
            userEntry.DeleteTree();
            userEntry.CommitChanges();
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Delete user: {samAccountName}", ex);
            throw new InvalidOperationException($"Failed to delete user: {ex.Message}", ex);
        }
    }

    private ADUserModel MapSearchResult(SearchResult result)
    {
        var props = result.Properties;

        var uac = GetPropertyValue<int>(props, "userAccountControl");
        var lockoutTime = GetPropertyValue<long>(props, "lockoutTime");
        var pwdLastSet = GetPropertyValue<long>(props, "pwdLastSet");
        var lastLogon = GetPropertyValue<long>(props, "lastLogon");

        var dn = GetPropertyValue<string>(props, "distinguishedName") ?? "";
        var location = TerminationService.ParseOUFromDN(dn) ?? "";

        return new ADUserModel
        {
            SamAccountName = GetPropertyValue<string>(props, "sAMAccountName") ?? "",
            DisplayName = GetPropertyValue<string>(props, "displayName") ?? "",
            FirstName = GetPropertyValue<string>(props, "givenName") ?? "",
            LastName = GetPropertyValue<string>(props, "sn") ?? "",
            Email = GetPropertyValue<string>(props, "mail") ?? "",
            Department = GetPropertyValue<string>(props, "department") ?? "",
            Title = GetPropertyValue<string>(props, "title") ?? "",
            Description = GetPropertyValue<string>(props, "description") ?? "",
            DistinguishedName = dn,
            OrganizationalUnit = location,
            IsEnabled = (uac & 0x0002) == 0,
            IsLockedOut = lockoutTime > 0,
            PasswordLastSet = FileTimeToDateTime(pwdLastSet),
            LastLogon = FileTimeToDateTime(lastLogon),
            UserPrincipalName = GetPropertyValue<string>(props, "userPrincipalName") ?? "",
            DirectoryType = DirectoryType.OnPremisesAD
        };
    }

    private DirectoryEntry? FindUserEntryBySamAccountName(string samAccountName)
    {
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = $"(&(objectClass=user)(objectCategory=person)(sAMAccountName={EscapeLdapFilter(samAccountName)}))",
            SearchScope = SearchScope.Subtree
        };

        var result = searcher.FindOne();
        return result?.GetDirectoryEntry();
    }

    private bool EmailExistsForAnotherUser(string email, Guid currentUserGuid)
    {
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = $"(&(objectClass=user)(objectCategory=person)(|(mail={EscapeLdapFilter(email)})(userPrincipalName={EscapeLdapFilter(email)})))",
            SearchScope = SearchScope.Subtree
        };

        foreach (SearchResult result in searcher.FindAll())
        {
            using var candidateEntry = result.GetDirectoryEntry();
            if (candidateEntry.Guid != currentUserGuid)
            {
                return true;
            }
        }

        return false;
    }

    private static void SetRequiredProperty(DirectoryEntry entry, string propertyName, string value)
    {
        entry.Properties[propertyName].Value = value.Trim();
    }

    private static void SetOptionalProperty(DirectoryEntry entry, string propertyName, string value)
    {
        var trimmed = value.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            entry.Properties[propertyName].Clear();
            return;
        }

        entry.Properties[propertyName].Value = trimmed;
    }

    private static void SetAccountEnabled(DirectoryEntry entry, bool enabled)
    {
        var currentValue = entry.Properties["userAccountControl"].Value;
        var uac = currentValue is int intValue ? intValue : 0x0200;

        if (enabled)
        {
            uac &= ~0x0002;
        }
        else
        {
            uac |= 0x0002;
        }

        entry.Properties["userAccountControl"].Value = uac;
    }

    internal static string? ValidateUpdateRequest(UpdateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalSamAccountName))
            return "Failed to determine which user is being edited.";
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "Enter a first name.";
        if (string.IsNullOrWhiteSpace(request.LastName))
            return "Enter a last name.";
        if (string.IsNullOrWhiteSpace(request.Email))
            return "Enter an email address.";
        if (string.IsNullOrWhiteSpace(request.TargetOU))
            return "Select an OU.";
        if (!UserProvisioningService.IsValidEmail(request.Email))
            return "Enter a valid email address.";

        return null;
    }

    internal static bool HasEmailChanged(string currentEmail, string requestedEmail)
    {
        return !string.Equals(currentEmail.Trim(), requestedEmail.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    internal static GroupMembershipChangeSet CalculateGroupMembershipChanges(
        IEnumerable<string> currentGroups,
        IEnumerable<string> requestedGroups)
    {
        var currentSet = currentGroups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var requestedSet = requestedGroups
            .Where(group => !string.IsNullOrWhiteSpace(group))
            .Select(group => group.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = new GroupMembershipChangeSet();
        result.GroupsToAdd.AddRange(requestedSet.Where(group => !currentSet.Contains(group)).OrderBy(group => group));
        result.GroupsToRemove.AddRange(currentSet.Where(group => !requestedSet.Contains(group)).OrderBy(group => group));
        return result;
    }

    public async Task<List<ADUserModel>> SearchUsersAsync(string filter)
    {
        return await Task.Run(() => SearchUsers(filter));
    }

    public async Task<ADUserModel?> GetUserAsync(string identifier)
    {
        return await Task.Run(() => GetUser(identifier));
    }
}
