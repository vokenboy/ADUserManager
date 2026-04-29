namespace ActiveManager.Services.Models;

public class ADUserModel
{
    // Common properties (both AD types)
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTime? PasswordLastSet { get; set; }
    public DateTime? LastLogon { get; set; }
    public List<string> Groups { get; set; } = new();

    // On-Premises AD specific properties (null for Azure AD)
    public string DistinguishedName { get; set; } = string.Empty;
    public string? OrganizationalUnit { get; set; } = string.Empty;

    // Azure AD specific properties (empty for On-Premises AD)
    public string ObjectId { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;

    // Directory type tracking
    public DirectoryType DirectoryType { get; set; } = DirectoryType.OnPremisesAD;
}
