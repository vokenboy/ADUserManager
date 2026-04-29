using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public class TerminationService : ActiveDirectoryBase, IDirectoryTerminationService, ITerminationService
{
    /// <summary>
    /// Disable a user account by setting the ACCOUNTDISABLE flag (0x0002) in userAccountControl.
    /// </summary>
    public void DisableUser(string distinguishedName)
    {
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        var uac = (int)userEntry.Properties["userAccountControl"].Value!;
        uac |= 0x0002; // ADS_UF_ACCOUNTDISABLE
        userEntry.Properties["userAccountControl"].Value = uac;
        userEntry.CommitChanges();
    }

    /// <summary>
    /// Move a user to a target OU.
    /// </summary>
    public void MoveUser(string distinguishedName, string targetOU)
    {
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        using var targetEntry = new DirectoryEntry($"LDAP://{targetOU}");
        userEntry.MoveTo(targetEntry);
    }

    /// <summary>
    /// Reset user password to a new random password.
    /// </summary>
    public void ResetPassword(string samAccountName, string newPassword)
    {
        ResetPassword(samAccountName, newPassword, forcePasswordChangeAtNextLogon: false);
    }

    /// <summary>
    /// Reset user password and optionally require password change at next sign-in.
    /// </summary>
    public void ResetPassword(string samAccountName, string newPassword, bool forcePasswordChangeAtNextLogon)
    {
        using var user = UserPrincipal.FindByIdentity(_context, IdentityType.SamAccountName, samAccountName);
        if (user == null)
            throw new InvalidOperationException($"Vartotojas '{samAccountName}' nerastas AD.");

        user.SetPassword(newPassword);
        user.Save();

        if (!forcePasswordChangeAtNextLogon)
        {
            return;
        }

        var distinguishedName = user.DistinguishedName;
        if (string.IsNullOrWhiteSpace(distinguishedName))
        {
            throw new InvalidOperationException($"Vartotojo '{samAccountName}' distinguished name nerastas.");
        }

        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        userEntry.Properties["pwdLastSet"].Value = 0;
        userEntry.CommitChanges();
    }

    /// <summary>
    /// Set account expiration date on a user.
    /// </summary>
    public void SetAccountExpiration(string distinguishedName, DateTime expirationDate)
    {
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        // accountExpires uses Windows FILETIME (100-nanosecond intervals since 1601-01-01)
        var fileTime = expirationDate.Date.AddDays(1).ToFileTime(); // End of the expiration day
        userEntry.Properties["accountExpires"].Value = fileTime.ToString();
        userEntry.CommitChanges();
    }

    /// <summary>
    /// Clear account expiration (set to never expire).
    /// </summary>
    public void ClearAccountExpiration(string distinguishedName)
    {
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        userEntry.Properties["accountExpires"].Value = "0";
        userEntry.CommitChanges();
    }

    /// <summary>
    /// Enable a user account by clearing the ACCOUNTDISABLE flag.
    /// </summary>
    public void EnableUser(string distinguishedName)
    {
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");
        var uac = (int)userEntry.Properties["userAccountControl"].Value!;
        uac &= ~0x0002; // Clear ADS_UF_ACCOUNTDISABLE
        userEntry.Properties["userAccountControl"].Value = uac;
        userEntry.CommitChanges();
    }

    /// <summary>
    /// Add a user back to specified groups.
    /// </summary>
    public void AddToGroups(string distinguishedName, List<GroupMembershipRecord> groups)
    {
        foreach (var group in groups)
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{group.GroupDN}");
                groupEntry.Properties["member"].Add(distinguishedName);
                groupEntry.CommitChanges();
            }
            catch (Exception ex)
            {
                // Skip if user is already a member or group doesn't exist
                if (!ex.Message.Contains("already a member", StringComparison.OrdinalIgnoreCase) &&
                    !ex.Message.Contains("0x80071392", StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Parse the original OU from a DistinguishedName.
    /// E.g., "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt" -> "OU=IT,DC=company,DC=lt"
    /// </summary>
    public static string? ParseOUFromDN(string distinguishedName)
    {
        if (string.IsNullOrEmpty(distinguishedName)) return null;
        var commaIndex = distinguishedName.IndexOf(',');
        if (commaIndex < 0) return null;
        return distinguishedName[(commaIndex + 1)..];
    }

    /// <summary>
    /// Get all group memberships for a user (excluding Domain Users).
    /// Returns a list of group name + DN pairs.
    /// </summary>
    public List<GroupMembershipRecord> GetUserGroups(string distinguishedName)
    {
        var groups = new List<GroupMembershipRecord>();
        using var userEntry = new DirectoryEntry($"LDAP://{distinguishedName}");

        var memberOf = userEntry.Properties["memberOf"];
        if (memberOf != null)
        {
            foreach (var groupDn in memberOf)
            {
                var dn = groupDn?.ToString() ?? "";
                if (string.IsNullOrEmpty(dn)) continue;

                // Extract CN from DN
                var cnStart = dn.IndexOf("CN=", StringComparison.OrdinalIgnoreCase);
                var cnEnd = dn.IndexOf(',', cnStart >= 0 ? cnStart : 0);
                var groupName = cnStart >= 0 && cnEnd > cnStart
                    ? dn[(cnStart + 3)..cnEnd]
                    : dn;

                groups.Add(new GroupMembershipRecord
                {
                    GroupName = groupName,
                    GroupDN = dn
                });
            }
        }

        return groups;
    }

    /// <summary>
    /// Remove user from all groups (except Domain Users which is the primary group).
    /// </summary>
    public void RemoveFromAllGroups(string distinguishedName)
    {
        var groups = GetUserGroups(distinguishedName);

        foreach (var group in groups)
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{group.GroupDN}");
                groupEntry.Properties["member"].Remove(distinguishedName);
                groupEntry.CommitChanges();
            }
            catch (Exception ex)
            {
                // Skip groups that can't be modified (e.g., primary group)
                // but throw if it's an unexpected error
                if (!ex.Message.Contains("primary group", StringComparison.OrdinalIgnoreCase) &&
                    !ex.Message.Contains("0x80070561", StringComparison.OrdinalIgnoreCase))
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Get all Organizational Units from the domain for the OU dropdown.
    /// </summary>
    public List<string> GetOrganizationalUnits()
    {
        var ous = new List<string>();
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = "(objectClass=organizationalUnit)",
            SearchScope = SearchScope.Subtree,
            SizeLimit = 500
        };

        searcher.PropertiesToLoad.Add("distinguishedName");

        foreach (SearchResult result in searcher.FindAll())
        {
            var dn = result.Properties["distinguishedName"][0]?.ToString();
            if (!string.IsNullOrEmpty(dn))
            {
                ous.Add(dn);
            }
        }

        ous.Sort();
        return ous;
    }

    /// <summary>
    /// Returns valid placement targets for user create/edit flows.
    /// Includes all organizational units plus the built-in Users container.
    /// </summary>
    public List<string> GetUserPlacementTargets()
    {
        return BuildUserPlacementTargets(GetOrganizationalUnits(), GetDefaultNamingContext());
    }

    internal static List<string> BuildUserPlacementTargets(IEnumerable<string> organizationalUnits, string defaultNamingContext)
    {
        var placementTargets = organizationalUnits
            .Where(dn => !string.IsNullOrWhiteSpace(dn))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var usersContainerDn = BuildDefaultUsersContainerDn(defaultNamingContext);
        if (!string.IsNullOrWhiteSpace(usersContainerDn) &&
            !placementTargets.Contains(usersContainerDn, StringComparer.OrdinalIgnoreCase))
        {
            placementTargets.Add(usersContainerDn);
        }

        placementTargets.Sort(StringComparer.OrdinalIgnoreCase);
        return placementTargets;
    }

    internal static string BuildDefaultUsersContainerDn(string defaultNamingContext)
    {
        var trimmedContext = defaultNamingContext.Trim();
        return string.IsNullOrWhiteSpace(trimmedContext)
            ? string.Empty
            : $"CN=Users,{trimmedContext}";
    }

    private string GetDefaultNamingContext()
    {
        return _domainPath.StartsWith("LDAP://", StringComparison.OrdinalIgnoreCase)
            ? _domainPath["LDAP://".Length..]
            : _domainPath;
    }

    /// <summary>
    /// Export user data to a file in the specified format (JSON, CSV, XML).
    /// </summary>
    public void ExportUserData(ADUserModel user, List<GroupMembershipRecord> groups,
        string format, string filePath, bool includeGroups, bool includePermissions)
    {
        var exportData = new Dictionary<string, object>
        {
            ["VartotojoInfo"] = new
            {
                user.SamAccountName,
                user.DisplayName,
                user.FirstName,
                user.LastName,
                user.Email,
                user.Department,
                user.Title,
                user.Description,
                user.DistinguishedName,
                user.OrganizationalUnit,
                user.IsEnabled,
                user.IsLockedOut,
                PasswordLastSet = user.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm:ss"),
                LastLogon = user.LastLogon?.ToString("yyyy-MM-dd HH:mm:ss")
            },
            ["EksportoData"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["EksportavoVartotojas"] = Environment.UserName
        };

        if (includeGroups)
        {
            exportData["GrupiuNarystes"] = groups.Select(g => new
            {
                g.GroupName,
                g.GroupDN
            }).ToList();
        }

        if (includePermissions)
        {
            // Permissions are the same as group memberships in AD context
            exportData["Teises"] = groups.Select(g => g.GroupName).ToList();
        }

        switch (format.ToUpperInvariant())
        {
            case "JSON":
                ExportAsJson(exportData, filePath);
                break;
            case "CSV":
                ExportAsCsv(user, groups, filePath, includeGroups);
                break;
            case "XML":
                ExportAsXml(exportData, filePath);
                break;
            default:
                throw new ArgumentException($"Nepalaikomas formatas: {format}");
        }
    }

    internal static void ExportAsJson(Dictionary<string, object> data, string filePath)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    internal static void ExportAsCsv(ADUserModel user, List<GroupMembershipRecord> groups,
        string filePath, bool includeGroups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Laukas;Reikšmė");
        sb.AppendLine($"SamAccountName;{user.SamAccountName}");
        sb.AppendLine($"DisplayName;{user.DisplayName}");
        sb.AppendLine($"FirstName;{user.FirstName}");
        sb.AppendLine($"LastName;{user.LastName}");
        sb.AppendLine($"Email;{user.Email}");
        sb.AppendLine($"Department;{user.Department}");
        sb.AppendLine($"Title;{user.Title}");
        sb.AppendLine($"Description;{user.Description}");
        sb.AppendLine($"DistinguishedName;{user.DistinguishedName}");
        sb.AppendLine($"OrganizationalUnit;{user.OrganizationalUnit}");
        sb.AppendLine($"IsEnabled;{user.IsEnabled}");
        sb.AppendLine($"IsLockedOut;{user.IsLockedOut}");
        sb.AppendLine($"PasswordLastSet;{user.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
        sb.AppendLine($"LastLogon;{user.LastLogon?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");

        if (includeGroups)
        {
            sb.AppendLine();
            sb.AppendLine("Grupė;Distinguished Name");
            foreach (var g in groups)
            {
                sb.AppendLine($"{g.GroupName};{g.GroupDN}");
            }
        }

        File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
    }

    internal static void ExportAsXml(Dictionary<string, object> data, string filePath)
    {
        var doc = new XDocument(
            new XElement("VartotojoEksportas",
                new XElement("EksportoData", data["EksportoData"]),
                new XElement("EksportavoVartotojas", data["EksportavoVartotojas"]),
                SerializeToXElement("VartotojoInfo", data["VartotojoInfo"])
            )
        );

        if (data.ContainsKey("GrupiuNarystes"))
        {
            var groupsElement = new XElement("GrupiuNarystes");
            var jsonGroups = JsonSerializer.Serialize(data["GrupiuNarystes"]);
            var groups = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(jsonGroups);
            if (groups != null)
            {
                foreach (var g in groups)
                {
                    groupsElement.Add(new XElement("Grupe",
                        new XElement("Pavadinimas", g.GetValueOrDefault("GroupName", "")),
                        new XElement("DN", g.GetValueOrDefault("GroupDN", ""))
                    ));
                }
            }
            doc.Root!.Add(groupsElement);
        }

        doc.Save(filePath);
    }

    internal static XElement SerializeToXElement(string name, object obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        var element = new XElement(name);

        if (dict != null)
        {
            foreach (var kvp in dict)
            {
                element.Add(new XElement(kvp.Key, kvp.Value.ToString()));
            }
        }

        return element;
    }

    // =============================================
    // Memento Pattern - Originator Implementation
    // =============================================

    /// <summary>
    /// Memento pattern: Creates an immutable snapshot of the user's current AD state.
    /// Captures account status, OU, group memberships, and expiration.
    /// Should be called BEFORE any destructive termination operations.
    /// </summary>
    public UserMemento CreateMemento(ADUserModel user)
    {
        var groups = GetUserGroups(user.DistinguishedName);

        DateTime? accountExpiration = null;
        try
        {
            using var entry = new DirectoryEntry($"LDAP://{user.DistinguishedName}");
            var expiresValue = entry.Properties["accountExpires"].Value;
            if (expiresValue is long fileTime && fileTime > 0 && fileTime < long.MaxValue)
            {
                accountExpiration = DateTime.FromFileTime(fileTime);
            }
        }
        catch
        {
            // If we can't read expiration, leave it as null
        }

        return new UserMemento(
            samAccountName: user.SamAccountName,
            displayName: user.DisplayName,
            distinguishedName: user.DistinguishedName,
            organizationalUnit: user.OrganizationalUnit,
            isEnabled: user.IsEnabled,
            isLockedOut: user.IsLockedOut,
            accountExpiration: accountExpiration,
            groupMemberships: groups,
            capturedAt: DateTime.Now,
            capturedBy: Environment.UserName,
            directoryType: DirectoryType.OnPremisesAD,
            objectId: null
        );
    }

    /// <summary>
    /// Memento pattern: Restores a user's AD state from a previously captured memento.
    /// Re-enables account, moves back to original OU, restores group memberships,
    /// and clears account expiration. Password is NOT restored (by design - security).
    /// </summary>
    public void RestoreFromMemento(UserMemento memento)
    {
        // Validate directory type
        if (memento.DirectoryType != DirectoryType.OnPremisesAD)
        {
            throw new InvalidOperationException(
                $"Cannot restore {memento.DirectoryType} memento to On-Premises AD directory. " +
                "Mementos can only be restored to the same directory type they were created from.");
        }

        // Find the user's current DN (may have changed if they were moved)
        var currentDN = FindUserDN(memento.SamAccountName)
            ?? throw new InvalidOperationException(
                $"Vartotojas '{memento.SamAccountName}' nerastas AD.");

        // Restore enabled state
        if (memento.IsEnabled)
        {
            EnableUser(currentDN);
        }

        // Move back to original OU
        var originalOU = memento.OrganizationalUnit;
        if (string.IsNullOrEmpty(originalOU))
        {
            originalOU = ParseOUFromDN(memento.DistinguishedName);
        }

        if (!string.IsNullOrEmpty(originalOU))
        {
            var currentOU = ParseOUFromDN(currentDN);
            if (!string.Equals(currentOU, originalOU, StringComparison.OrdinalIgnoreCase))
            {
                MoveUser(currentDN, originalOU);
                // Update currentDN after move
                var cn = currentDN.Split(',')[0];
                currentDN = $"{cn},{originalOU}";
            }
        }

        // Restore group memberships
        if (memento.GroupMemberships.Count > 0)
        {
            AddToGroups(currentDN, memento.GroupMemberships);
        }

        // Restore account expiration
        if (memento.AccountExpiration.HasValue)
        {
            SetAccountExpiration(currentDN, memento.AccountExpiration.Value);
        }
        else
        {
            ClearAccountExpiration(currentDN);
        }
    }

    /// <summary>
    /// Find a user's current Distinguished Name by their SamAccountName.
    /// Returns null if the user is not found.
    /// </summary>
    public string? FindUserDN(string samAccountName)
    {
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = $"(sAMAccountName={samAccountName})",
            SearchScope = SearchScope.Subtree
        };
        searcher.PropertiesToLoad.Add("distinguishedName");

        var result = searcher.FindOne();
        return result?.Properties["distinguishedName"][0]?.ToString();
    }

    // IDirectoryTerminationService async implementations
    public async Task<bool> DisableUserAsync(string identifier)
    {
        return await Task.Run(() =>
        {
            try
            {
                DisableUser(identifier);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> EnableUserAsync(string identifier)
    {
        return await Task.Run(() =>
        {
            try
            {
                EnableUser(identifier);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> ResetPasswordAsync(string identifier, string newPassword)
    {
        return await Task.Run(() =>
        {
            try
            {
                ResetPassword(identifier, newPassword);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> AddToGroupAsync(string userIdentifier, string groupName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{groupName}");
                groupEntry.Properties["member"].Add(userIdentifier);
                groupEntry.CommitChanges();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> RemoveFromGroupAsync(string userIdentifier, string groupName)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{groupName}");
                groupEntry.Properties["member"].Remove(userIdentifier);
                groupEntry.CommitChanges();
                return true;
            }
            catch
            {
                return false;
            }
        });
    }

    public async Task<bool> RemoveFromAllGroupsAsync(string identifier)
    {
        return await Task.Run(() =>
        {
            try
            {
                RemoveFromAllGroups(identifier);
                return true;
            }
            catch
            {
                return false;
            }
        });
    }
}
