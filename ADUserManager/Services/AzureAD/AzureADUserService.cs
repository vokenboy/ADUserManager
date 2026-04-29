using Microsoft.Graph;
using Microsoft.Graph.Users;
using Microsoft.Kiota.Abstractions;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services.AzureAD;

/// <summary>
/// Azure AD implementation of user search and retrieval operations.
/// </summary>
public class AzureADUserService : AzureADBase, IDirectoryUserService
{
    public AzureADUserService(AzureADSettings settings) : base(settings)
    {
    }

    /// <summary>
    /// Searches for users in Azure AD matching the specified filter.
    /// Searches across displayName, userPrincipalName, and mail fields.
    /// </summary>
    public async Task<List<ADUserModel>> SearchUsersAsync(string filter)
    {
        var users = new List<ADUserModel>();

        try
        {
            var requestConfig = (RequestConfiguration<UsersRequestBuilder.UsersRequestBuilderGetQueryParameters> req) =>
            {
                // Build filter for multiple fields
                req.QueryParameters.Filter = $"startswith(displayName,'{filter}') or startswith(userPrincipalName,'{filter}') or startswith(mail,'{filter}')";

                // Select only needed fields for performance
                req.QueryParameters.Select = new[]
                {
                    "id", "displayName", "givenName", "surname",
                    "mail", "userPrincipalName", "accountEnabled",
                    "department", "jobTitle", "memberOf"
                };

                req.QueryParameters.Top = 1000;
            };

            var response = await _graphClient.Users.GetAsync(requestConfig);

            if (response?.Value != null)
            {
                foreach (var user in response.Value)
                {
                    users.Add(MapGraphUserToModel(user));
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD user search: {filter}", ex);
            throw;
        }

        return users;
    }

    /// <summary>
    /// Retrieves detailed information about a specific user.
    /// Supports lookup by Object ID or User Principal Name.
    /// </summary>
    public async Task<ADUserModel?> GetUserAsync(string identifier)
    {
        try
        {
            Microsoft.Graph.Models.User? user = null;

            // Try to get user by ID first (if it looks like a GUID)
            if (Guid.TryParse(identifier, out _))
            {
                user = await _graphClient.Users[identifier].GetAsync(req =>
                {
                    req.QueryParameters.Select = new[]
                    {
                        "id", "displayName", "givenName", "surname",
                        "mail", "userPrincipalName", "accountEnabled",
                        "department", "jobTitle", "companyName"
                    };
                });
            }
            else
            {
                // Search by UPN or mail
                var filter = $"userPrincipalName eq '{identifier}' or mail eq '{identifier}'";
                var response = await _graphClient.Users.GetAsync(req =>
                {
                    req.QueryParameters.Filter = filter;
                    req.QueryParameters.Select = new[]
                    {
                        "id", "displayName", "givenName", "surname",
                        "mail", "userPrincipalName", "accountEnabled",
                        "department", "jobTitle", "companyName"
                    };
                });

                user = response?.Value?.FirstOrDefault();
            }

            if (user == null)
                return null;

            var model = MapGraphUserToModel(user);

            // Get group memberships
            try
            {
                var groups = await _graphClient.Users[user.Id].MemberOf.GetAsync();
                if (groups?.Value != null)
                {
                    model.Groups = groups.Value
                        .OfType<Microsoft.Graph.Models.Group>()
                        .Select(g => g.DisplayName ?? "")
                        .Where(name => !string.IsNullOrEmpty(name))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log($"Azure AD get user groups: {identifier}", ex);
            }

            return model;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD get user: {identifier}", ex);
            throw;
        }
    }

    /// <summary>
    /// Maps a Microsoft Graph User object to ADUserModel.
    /// </summary>
    private ADUserModel MapGraphUserToModel(Microsoft.Graph.Models.User user)
    {
        return new ADUserModel
        {
            // Azure AD specific
            ObjectId = user.Id ?? "",
            UserPrincipalName = user.UserPrincipalName ?? "",
            DirectoryType = DirectoryType.AzureAD,
            OrganizationalUnit = null, // Azure AD doesn't have OUs

            // Common fields
            SamAccountName = user.UserPrincipalName?.Split('@')[0] ?? "",
            DisplayName = user.DisplayName ?? "",
            FirstName = user.GivenName ?? "",
            LastName = user.Surname ?? "",
            Email = user.Mail ?? user.UserPrincipalName ?? "",
            Department = user.Department ?? "",
            Title = user.JobTitle ?? "",
            IsEnabled = user.AccountEnabled ?? false,

            // Fields that require additional API calls (not fetched in basic search)
            // LastLogon and PasswordLastSet would need separate signInActivity calls
            IsLockedOut = false, // Azure AD handles this differently via Conditional Access
            DistinguishedName = "" // Not applicable to Azure AD
        };
    }
}
