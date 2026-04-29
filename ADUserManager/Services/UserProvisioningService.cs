using System.DirectoryServices;
using System.Text.RegularExpressions;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public class UserProvisioningService : ActiveDirectoryBase, IDirectoryUserProvisioningService
{
    private readonly UserService _userService;

    public UserProvisioningService() : this(new UserService())
    {
    }

    internal UserProvisioningService(UserService userService)
    {
        _userService = userService;
    }

    public async Task<CreateUserResult> CreateUserAsync(CreateUserRequest request)
    {
        return await Task.Run(() => CreateUser(request));
    }

    private CreateUserResult CreateUser(CreateUserRequest request)
    {
        var validationError = ValidateRequest(request);
        if (!string.IsNullOrEmpty(validationError))
        {
            return new CreateUserResult { ErrorMessage = validationError };
        }

        var samAccountName = request.SamAccountName.Trim();
        var email = request.Email.Trim();

        if (UserExistsBySamAccountName(samAccountName) || EmailExists(email))
        {
            return new CreateUserResult
            {
                ErrorMessage = BuildUniquenessError(
                    UserExistsBySamAccountName(samAccountName),
                    EmailExists(email))
            };
        }

        var generatedPassword = GenerateSecurePassword();
        var displayName = BuildDisplayName(request.FirstName, request.LastName);
        var selectedGroups = request.SelectedGroups
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DirectoryEntry? userEntry = null;

        try
        {
            using var ouEntry = new DirectoryEntry($"LDAP://{request.TargetOU.Trim()}");
            userEntry = ouEntry.Children.Add($"CN={EscapeLdapRdn(displayName)}", "user");

            userEntry.Properties["sAMAccountName"].Value = samAccountName;
            userEntry.Properties["userPrincipalName"].Value = email;
            userEntry.Properties["givenName"].Value = request.FirstName.Trim();
            userEntry.Properties["sn"].Value = request.LastName.Trim();
            userEntry.Properties["displayName"].Value = displayName;
            userEntry.Properties["mail"].Value = email;

            SetOptionalProperty(userEntry, "department", request.Department);
            SetOptionalProperty(userEntry, "title", request.Title);
            SetOptionalProperty(userEntry, "description", request.Description);

            userEntry.CommitChanges();
            userEntry.Invoke("SetPassword", generatedPassword);
            userEntry.Properties["pwdLastSet"].Value = request.ForcePasswordChangeAtNextLogon ? 0 : -1;
            userEntry.Properties["userAccountControl"].Value = request.Enabled ? 0x0200 : 0x0202;
            userEntry.CommitChanges();

            var warnings = AddToGroups(userEntry.Properties["distinguishedName"].Value?.ToString(), selectedGroups);
            var createdUser = _userService.GetUser(samAccountName);

            return new CreateUserResult
            {
                IsSuccess = warnings.Count == 0,
                UserCreated = true,
                User = createdUser,
                GeneratedPassword = generatedPassword,
                ErrorMessage = warnings.Count == 0
                    ? string.Empty
                    : $"User created, but some group assignments failed: {string.Join(", ", warnings)}",
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            try
            {
                userEntry?.DeleteTree();
                userEntry?.CommitChanges();
            }
            catch
            {
            }

            ErrorLogger.Log($"Create user: {samAccountName}", ex);
            return new CreateUserResult
            {
                ErrorMessage = $"Failed to create user: {ex.Message}"
            };
        }
        finally
        {
            userEntry?.Dispose();
        }
    }

    private static void SetOptionalProperty(DirectoryEntry entry, string propertyName, string value)
    {
        var trimmed = value.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            entry.Properties[propertyName].Value = trimmed;
        }
    }

    private List<string> AddToGroups(string? userDistinguishedName, List<string> groups)
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(userDistinguishedName))
        {
            warnings.Add("Failed to determine the user DN");
            return warnings;
        }

        foreach (var groupDn in groups)
        {
            try
            {
                using var groupEntry = new DirectoryEntry($"LDAP://{groupDn}");
                groupEntry.Properties["member"].Add(userDistinguishedName);
                groupEntry.CommitChanges();
            }
            catch (Exception ex)
            {
                ErrorLogger.Log($"Add to group: {groupDn}", ex);
                warnings.Add(groupDn);
            }
        }

        return warnings;
    }

    private bool UserExistsBySamAccountName(string samAccountName)
    {
        return FindUserByAttribute("sAMAccountName", samAccountName) != null;
    }

    private bool EmailExists(string email)
    {
        return FindUserByAttribute("mail", email) != null || FindUserByAttribute("userPrincipalName", email) != null;
    }

    private SearchResult? FindUserByAttribute(string attributeName, string value)
    {
        using var entry = new DirectoryEntry(_domainPath);
        using var searcher = new DirectorySearcher(entry)
        {
            Filter = $"(&(objectClass=user)(objectCategory=person)({attributeName}={EscapeLdapFilter(value)}))",
            SearchScope = SearchScope.Subtree
        };

        return searcher.FindOne();
    }

    internal static string? ValidateRequest(CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return "Enter a first name.";
        if (string.IsNullOrWhiteSpace(request.LastName))
            return "Enter a last name.";
        if (string.IsNullOrWhiteSpace(request.SamAccountName))
            return "Enter a username.";
        if (string.IsNullOrWhiteSpace(request.Email))
            return "Enter an email address.";
        if (string.IsNullOrWhiteSpace(request.TargetOU))
            return "Select an OU.";
        if (!IsValidEmail(request.Email))
            return "Enter a valid email address.";

        return null;
    }

    internal static string BuildDisplayName(string firstName, string lastName)
    {
        return $"{firstName.Trim()} {lastName.Trim()}".Trim();
    }

    internal static string BuildUniquenessError(bool samExists, bool emailExists)
    {
        if (samExists && emailExists)
            return "The username and email address are already in use.";
        if (samExists)
            return "That username is already in use.";
        if (emailExists)
            return "That email address is already in use.";

        return string.Empty;
    }

    internal static bool IsValidEmail(string email)
    {
        return Regex.IsMatch(email.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$");
    }

    internal static string GenerateSecurePassword(int length = 16)
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";
        var all = upper + lower + digits + special;

        var password = new List<char>
        {
            upper[Random.Shared.Next(upper.Length)],
            lower[Random.Shared.Next(lower.Length)],
            digits[Random.Shared.Next(digits.Length)],
            special[Random.Shared.Next(special.Length)]
        };

        while (password.Count < length)
        {
            password.Add(all[Random.Shared.Next(all.Length)]);
        }

        return new string(password.OrderBy(_ => Random.Shared.Next()).ToArray());
    }

    internal static string EscapeLdapRdn(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace(",", "\\,")
            .Replace("+", "\\+")
            .Replace("\"", "\\\"")
            .Replace("<", "\\<")
            .Replace(">", "\\>")
            .Replace(";", "\\;")
            .Replace("=", "\\=")
            .Trim();
    }
}
