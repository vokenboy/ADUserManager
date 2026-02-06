using System.DirectoryServices;
using ADUserManager.Services.Models;

namespace ADUserManager.Services;

public class GroupService : ActiveDirectoryBase
{
    public List<ADGroupModel> SearchGroups(string filter)
    {
        var groups = new List<ADGroupModel>();
        using var entry = new DirectoryEntry(_domainPath);

        var ldapFilter = string.IsNullOrWhiteSpace(filter)
            ? "(objectClass=group)"
            : $"(&(objectClass=group)(|(cn=*{EscapeLdapFilter(filter)}*)(description=*{EscapeLdapFilter(filter)}*)))";

        using var searcher = new DirectorySearcher(entry)
        {
            Filter = ldapFilter,
            SearchScope = SearchScope.Subtree,
            SizeLimit = 1000
        };

        searcher.PropertiesToLoad.AddRange(new[]
        {
            "cn", "distinguishedName", "description", "groupType", "member"
        });

        foreach (SearchResult result in searcher.FindAll())
        {
            groups.Add(MapGroupResult(result));
        }

        return groups;
    }

    public List<string> GetGroupMembers(string groupDn)
    {
        var members = new List<string>();
        using var groupEntry = new DirectoryEntry($"LDAP://{groupDn}");

        if (groupEntry.Properties["member"] != null)
        {
            foreach (var member in groupEntry.Properties["member"])
            {
                members.Add(member?.ToString() ?? "");
            }
        }

        return members;
    }

    private ADGroupModel MapGroupResult(SearchResult result)
    {
        var props = result.Properties;
        var groupType = GetPropertyValue<int>(props, "groupType");

        var scope = groupType switch
        {
            -2147483646 => "Global",
            -2147483644 => "Domain Local",
            -2147483640 => "Universal",
            2 => "Global (Distribution)",
            4 => "Domain Local (Distribution)",
            8 => "Universal (Distribution)",
            _ => "Unknown"
        };

        var category = groupType < 0 ? "Security" : "Distribution";
        var memberCount = props.Contains("member") ? props["member"].Count : 0;

        return new ADGroupModel
        {
            Name = GetPropertyValue<string>(props, "cn") ?? "",
            DistinguishedName = GetPropertyValue<string>(props, "distinguishedName") ?? "",
            Description = GetPropertyValue<string>(props, "description") ?? "",
            GroupScope = scope,
            GroupCategory = category,
            MemberCount = memberCount
        };
    }
}
