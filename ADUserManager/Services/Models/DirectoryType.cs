namespace ActiveManager.Services.Models;

/// <summary>
/// Defines the types of directory services supported by the application.
/// </summary>
public enum DirectoryType
{
    /// <summary>
    /// On-premises Active Directory Domain Services
    /// </summary>
    OnPremisesAD,

    /// <summary>
    /// Azure Active Directory (Microsoft Entra ID)
    /// </summary>
    AzureAD
}
