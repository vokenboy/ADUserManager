using System.Security.Principal;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public sealed class CurrentUserProfileService
{
    private ADUserModel? _cachedUser;

    public string WindowsUserName => Environment.UserName;

    public IReadOnlyList<string> CandidateIdentifiers { get; } = BuildCandidateIdentifiers();

    public ADUserModel? GetCurrentUser(UserService? userService, bool forceRefresh = false)
    {
        if (!forceRefresh && _cachedUser != null)
        {
            return _cachedUser;
        }

        if (userService == null)
        {
            return null;
        }

        foreach (var identifier in CandidateIdentifiers)
        {
            var user = userService.GetUserByIdentity(identifier);
            if (user != null)
            {
                _cachedUser = user;
                return user;
            }
        }

        return null;
    }

    public void SetCachedUser(ADUserModel? user)
    {
        _cachedUser = user;
    }

    public bool IsCurrentUser(ADUserModel user)
    {
        return MatchesAnyIdentity(user, CandidateIdentifiers);
    }

    internal static bool MatchesAnyIdentity(ADUserModel user, IEnumerable<string> candidateIdentifiers)
    {
        var userIdentifiers = CollectIdentifiers(user).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidateIdentifiers
            .Where(identifier => !string.IsNullOrWhiteSpace(identifier))
            .Select(identifier => identifier.Trim())
            .Any(userIdentifiers.Contains);
    }

    private static IReadOnlyList<string> BuildCandidateIdentifiers()
    {
        var identifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIdentifier(identifiers, Environment.UserName);
        AddIdentifier(identifiers, Environment.GetEnvironmentVariable("USERNAME"));
        AddIdentifier(identifiers, WindowsIdentity.GetCurrent()?.Name);

        var userDnsDomain = Environment.GetEnvironmentVariable("USERDNSDOMAIN");
        if (!string.IsNullOrWhiteSpace(userDnsDomain) && !string.IsNullOrWhiteSpace(Environment.UserName))
        {
            AddIdentifier(identifiers, $"{Environment.UserName}@{userDnsDomain}");
        }

        return identifiers.ToList();
    }

    private static IEnumerable<string> CollectIdentifiers(ADUserModel user)
    {
        yield return user.SamAccountName;
        yield return user.UserPrincipalName;
        yield return user.Email;

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            var atIndex = user.Email.IndexOf('@');
            if (atIndex > 0)
            {
                yield return user.Email[..atIndex];
            }
        }
    }

    private static void AddIdentifier(HashSet<string> identifiers, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        identifiers.Add(trimmed);

        var slashIndex = trimmed.LastIndexOf('\\');
        if (slashIndex >= 0 && slashIndex < trimmed.Length - 1)
        {
            identifiers.Add(trimmed[(slashIndex + 1)..]);
        }

        var atIndex = trimmed.IndexOf('@');
        if (atIndex > 0)
        {
            identifiers.Add(trimmed[..atIndex]);
        }
    }
}
