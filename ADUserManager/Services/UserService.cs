using System.DirectoryServices;
using ADUserManager.Services.Models;

namespace ADUserManager.Services;

public class UserService : ActiveDirectoryBase
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

    private ADUserModel MapSearchResult(SearchResult result)
    {
        var props = result.Properties;

        var uac = GetPropertyValue<int>(props, "userAccountControl");
        var lockoutTime = GetPropertyValue<long>(props, "lockoutTime");
        var pwdLastSet = GetPropertyValue<long>(props, "pwdLastSet");
        var lastLogon = GetPropertyValue<long>(props, "lastLogon");

        var dn = GetPropertyValue<string>(props, "distinguishedName") ?? "";
        var ouIndex = dn.IndexOf(",OU=", StringComparison.OrdinalIgnoreCase);
        var ou = ouIndex >= 0 ? dn[(ouIndex + 1)..] : "";

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
            OrganizationalUnit = ou,
            IsEnabled = (uac & 0x0002) == 0,
            IsLockedOut = lockoutTime > 0,
            PasswordLastSet = FileTimeToDateTime(pwdLastSet),
            LastLogon = FileTimeToDateTime(lastLogon)
        };
    }
}
