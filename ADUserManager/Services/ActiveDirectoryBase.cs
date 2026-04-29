using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public abstract class ActiveDirectoryBase : IDirectoryService
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
    public string DomainPath => _domainPath;
    public DirectoryType Type => DirectoryType.OnPremisesAD;

    /// <summary>
    /// Tests the connection to the on-premises Active Directory.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Try to query the domain
                using var searcher = new DirectorySearcher(new DirectoryEntry(_domainPath))
                {
                    Filter = "(objectClass=domain)",
                    SearchScope = SearchScope.Base
                };
                var result = searcher.FindOne();
                return result != null;
            }
            catch
            {
                return false;
            }
        });
    }

    protected static T? GetPropertyValue<T>(ResultPropertyCollection props, string name)
    {
        if (props.Contains(name) && props[name].Count > 0)
            return (T)props[name][0];
        return default;
    }

    internal static DateTime? FileTimeToDateTime(long fileTime)
    {
        if (fileTime <= 0 || fileTime == long.MaxValue)
            return null;
        try { return DateTime.FromFileTime(fileTime); }
        catch { return null; }
    }

    internal static string EscapeLdapFilter(string input)
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
