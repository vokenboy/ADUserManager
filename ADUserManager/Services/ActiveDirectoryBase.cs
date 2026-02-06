using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;

namespace ADUserManager.Services;

public abstract class ActiveDirectoryBase : IDisposable
{
    protected readonly PrincipalContext _context;
    protected readonly string _domainPath;

    protected ActiveDirectoryBase()
    {
        _context = new PrincipalContext(ContextType.Domain);
        using var rootDse = new DirectoryEntry("LDAP://RootDSE");
        var defaultNamingContext = rootDse.Properties["defaultNamingContext"].Value?.ToString() ?? "";
        _domainPath = $"LDAP://{defaultNamingContext}";
    }

    public string DomainName => _context.ConnectedServer ?? "Unknown";

    protected static T? GetPropertyValue<T>(ResultPropertyCollection props, string name)
    {
        if (props.Contains(name) && props[name].Count > 0)
            return (T)props[name][0];
        return default;
    }

    protected static DateTime? FileTimeToDateTime(long fileTime)
    {
        if (fileTime <= 0 || fileTime == long.MaxValue)
            return null;
        try { return DateTime.FromFileTime(fileTime); }
        catch { return null; }
    }

    protected static string EscapeLdapFilter(string input)
    {
        return input
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
