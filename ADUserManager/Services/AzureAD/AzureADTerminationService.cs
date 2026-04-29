using Microsoft.Graph;
using Microsoft.Graph.Models;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services.AzureAD;

/// <summary>
/// Azure AD implementation of user account management and termination operations.
/// Implements the Memento pattern for state capture and rollback.
/// </summary>
public class AzureADTerminationService : AzureADBase, IDirectoryTerminationService
{
    public AzureADTerminationService(AzureADSettings settings) : base(settings)
    {
    }

    /// <summary>
    /// Disables a user account in Azure AD.
    /// </summary>
    public async Task<bool> DisableUserAsync(string identifier)
    {
        try
        {
            var userId = await ResolveUserIdAsync(identifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {identifier}");
            }

            var user = new User
            {
                AccountEnabled = false
            };

            await _graphClient.Users[userId].PatchAsync(user);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD disable user: {identifier}", ex);
            return false;
        }
    }

    /// <summary>
    /// Enables a user account in Azure AD.
    /// </summary>
    public async Task<bool> EnableUserAsync(string identifier)
    {
        try
        {
            var userId = await ResolveUserIdAsync(identifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {identifier}");
            }

            var user = new User
            {
                AccountEnabled = true
            };

            await _graphClient.Users[userId].PatchAsync(user);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD enable user: {identifier}", ex);
            return false;
        }
    }

    /// <summary>
    /// Resets a user's password in Azure AD.
    /// Forces password change on next sign-in.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string identifier, string newPassword)
    {
        try
        {
            var userId = await ResolveUserIdAsync(identifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {identifier}");
            }

            var user = new User
            {
                PasswordProfile = new PasswordProfile
                {
                    Password = newPassword,
                    ForceChangePasswordNextSignIn = true
                }
            };

            await _graphClient.Users[userId].PatchAsync(user);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD reset password: {identifier}", ex);
            return false;
        }
    }

    /// <summary>
    /// Adds a user to a group in Azure AD.
    /// </summary>
    public async Task<bool> AddToGroupAsync(string userIdentifier, string groupName)
    {
        try
        {
            var userId = await ResolveUserIdAsync(userIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {userIdentifier}");
            }

            var groupId = await ResolveGroupIdAsync(groupName);
            if (string.IsNullOrEmpty(groupId))
            {
                throw new InvalidOperationException($"Group not found: {groupName}");
            }

            var directoryObject = new ReferenceCreate
            {
                OdataId = $"https://graph.microsoft.com/v1.0/directoryObjects/{userId}"
            };

            await _graphClient.Groups[groupId].Members.Ref.PostAsync(directoryObject);
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD add to group: {userIdentifier} -> {groupName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Removes a user from a group in Azure AD.
    /// </summary>
    public async Task<bool> RemoveFromGroupAsync(string userIdentifier, string groupName)
    {
        try
        {
            var userId = await ResolveUserIdAsync(userIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {userIdentifier}");
            }

            var groupId = await ResolveGroupIdAsync(groupName);
            if (string.IsNullOrEmpty(groupId))
            {
                throw new InvalidOperationException($"Group not found: {groupName}");
            }

            await _graphClient.Groups[groupId].Members[userId].Ref.DeleteAsync();
            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD remove from group: {userIdentifier} -> {groupName}", ex);
            return false;
        }
    }

    /// <summary>
    /// Removes a user from all groups they are a member of.
    /// </summary>
    public async Task<bool> RemoveFromAllGroupsAsync(string identifier)
    {
        try
        {
            var userId = await ResolveUserIdAsync(identifier);
            if (string.IsNullOrEmpty(userId))
            {
                throw new InvalidOperationException($"User not found: {identifier}");
            }

            // Get all group memberships
            var memberOf = await _graphClient.Users[userId].MemberOf.GetAsync();

            if (memberOf?.Value == null)
                return true;

            var groups = memberOf.Value.OfType<Group>().ToList();

            // Remove from each group
            foreach (var group in groups)
            {
                try
                {
                    await _graphClient.Groups[group.Id].Members[userId].Ref.DeleteAsync();
                }
                catch (Exception ex)
                {
                    ErrorLogger.Log($"Azure AD remove from group {group.DisplayName}: {identifier}", ex);
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD remove from all groups: {identifier}", ex);
            return false;
        }
    }

    /// <summary>
    /// Creates a memento capturing the current state of a user.
    /// </summary>
    public UserMemento CreateMemento(ADUserModel user)
    {
        try
        {
            var groupMemberships = user.Groups.Select(g => new GroupMembershipRecord
            {
                GroupName = g,
                GroupDN = g // For Azure AD, we use group name for both fields
            }).ToList();

            return new UserMemento(
                samAccountName: user.SamAccountName,
                displayName: user.DisplayName,
                distinguishedName: user.ObjectId, // Store Object ID in DN field for Azure AD
                organizationalUnit: null, // Azure AD doesn't have OUs
                isEnabled: user.IsEnabled,
                isLockedOut: user.IsLockedOut,
                accountExpiration: null, // Azure AD handles expiration differently
                groupMemberships: groupMemberships,
                capturedAt: DateTime.UtcNow,
                capturedBy: Environment.UserName,
                directoryType: DirectoryType.AzureAD,
                objectId: user.ObjectId
            );
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD create memento: {user.SamAccountName}", ex);
            throw;
        }
    }

    /// <summary>
    /// Restores a user's state from a memento.
    /// Validates that the memento is from Azure AD before restoring.
    /// </summary>
    public void RestoreFromMemento(UserMemento memento)
    {
        if (memento.DirectoryType != DirectoryType.AzureAD)
        {
            throw new InvalidOperationException(
                $"Cannot restore {memento.DirectoryType} memento to Azure AD directory. " +
                "Mementos can only be restored to the same directory type they were created from.");
        }

        try
        {
            // Re-enable the account
            if (memento.IsEnabled)
            {
                EnableUserAsync(memento.ObjectId ?? memento.SamAccountName).Wait();
            }

            // Restore group memberships
            foreach (var membership in memento.GroupMemberships)
            {
                AddToGroupAsync(memento.ObjectId ?? memento.SamAccountName, membership.GroupName).Wait();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD restore from memento: {memento.SamAccountName}", ex);
            throw;
        }
    }

    /// <summary>
    /// Resolves a user identifier (UPN or Object ID) to an Object ID.
    /// </summary>
    private async Task<string?> ResolveUserIdAsync(string identifier)
    {
        try
        {
            if (Guid.TryParse(identifier, out _))
            {
                return identifier;
            }

            var filter = $"userPrincipalName eq '{identifier}' or mail eq '{identifier}'";
            var response = await _graphClient.Users.GetAsync(req =>
            {
                req.QueryParameters.Filter = filter;
                req.QueryParameters.Select = new[] { "id" };
            });

            return response?.Value?.FirstOrDefault()?.Id;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves a group name to an Object ID.
    /// </summary>
    private async Task<string?> ResolveGroupIdAsync(string groupName)
    {
        try
        {
            if (Guid.TryParse(groupName, out _))
            {
                return groupName;
            }

            var filter = $"displayName eq '{groupName}'";
            var response = await _graphClient.Groups.GetAsync(req =>
            {
                req.QueryParameters.Filter = filter;
                req.QueryParameters.Select = new[] { "id" };
            });

            return response?.Value?.FirstOrDefault()?.Id;
        }
        catch
        {
            return null;
        }
    }
}
