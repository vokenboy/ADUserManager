using Microsoft.Graph;
using Azure.Identity;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services.AzureAD;

/// <summary>
/// Base class for Azure AD service implementations.
/// Provides common Graph API client initialization and connection testing.
/// </summary>
public abstract class AzureADBase : IDirectoryService
{
    protected readonly GraphServiceClient _graphClient;
    protected readonly string _tenantId;

    protected AzureADBase(AzureADSettings settings)
    {
        if (string.IsNullOrEmpty(settings.TenantId) ||
            string.IsNullOrEmpty(settings.ClientId) ||
            string.IsNullOrEmpty(settings.EncryptedSecret))
        {
            throw new InvalidOperationException("Azure AD settings not configured. Please configure Tenant ID, Client ID, and Client Secret in Settings.");
        }

        try
        {
            var credential = new ClientSecretCredential(
                settings.TenantId,
                settings.ClientId,
                settings.DecryptSecret()
            );

            _graphClient = new GraphServiceClient(credential);
            _tenantId = settings.TenantId;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Azure AD initialization", ex);
            throw new InvalidOperationException("Failed to initialize Azure AD connection. Check credentials.", ex);
        }
    }

    /// <summary>
    /// Gets the tenant ID as the domain name.
    /// </summary>
    public string DomainName => _tenantId;

    /// <summary>
    /// Gets the directory type (Azure AD).
    /// </summary>
    public DirectoryType Type => DirectoryType.AzureAD;

    /// <summary>
    /// Tests the connection to Azure AD by attempting to fetch a single user.
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var result = await _graphClient.Users.GetAsync(req =>
                req.QueryParameters.Top = 1);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Azure AD connection test", ex);
            return false;
        }
    }

    /// <summary>
    /// Disposes of resources. GraphServiceClient v5.x doesn't require explicit disposal.
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
