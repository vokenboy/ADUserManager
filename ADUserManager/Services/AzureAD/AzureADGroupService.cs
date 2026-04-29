using Microsoft.Graph;
using Microsoft.Graph.Groups;
using Microsoft.Kiota.Abstractions;
using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;

namespace ActiveManager.Services.AzureAD;

/// <summary>
/// Azure AD implementation of group search and membership operations.
/// </summary>
public class AzureADGroupService : AzureADBase, IDirectoryGroupService
{
    public AzureADGroupService(AzureADSettings settings) : base(settings)
    {
    }

    /// <summary>
    /// Searches for groups in Azure AD matching the specified filter.
    /// </summary>
    public async Task<List<ADGroupModel>> SearchGroupsAsync(string filter)
    {
        var groups = new List<ADGroupModel>();

        try
        {
            var requestConfig = (RequestConfiguration<GroupsRequestBuilder.GroupsRequestBuilderGetQueryParameters> req) =>
            {
                req.QueryParameters.Filter = $"startswith(displayName,'{filter}') or startswith(mailNickname,'{filter}')";
                req.QueryParameters.Select = new[] { "id", "displayName", "description", "mailNickname" };
                req.QueryParameters.Top = 1000;
            };

            var response = await _graphClient.Groups.GetAsync(requestConfig);

            if (response?.Value != null)
            {
                foreach (var group in response.Value)
                {
                    groups.Add(new ADGroupModel
                    {
                        Name = group.DisplayName ?? "",
                        Description = group.Description ?? "",
                        DistinguishedName = group.Id ?? "" // Store Object ID in DN field for Azure AD
                    });
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD group search: {filter}", ex);
            throw;
        }

        return groups;
    }

    /// <summary>
    /// Retrieves all members of a specified group.
    /// </summary>
    /// <param name="groupName">Group display name or Object ID</param>
    public async Task<List<string>> GetGroupMembersAsync(string groupName)
    {
        var members = new List<string>();

        try
        {
            // First, find the group
            string? groupId = null;

            if (Guid.TryParse(groupName, out _))
            {
                groupId = groupName;
            }
            else
            {
                var filter = $"displayName eq '{groupName}'";
                var groupResponse = await _graphClient.Groups.GetAsync(req =>
                {
                    req.QueryParameters.Filter = filter;
                    req.QueryParameters.Select = new[] { "id" };
                });

                groupId = groupResponse?.Value?.FirstOrDefault()?.Id;
            }

            if (string.IsNullOrEmpty(groupId))
            {
                throw new InvalidOperationException($"Group not found: {groupName}");
            }

            // Get group members
            var membersResponse = await _graphClient.Groups[groupId].Members.GetAsync();

            if (membersResponse?.Value != null)
            {
                foreach (var member in membersResponse.Value)
                {
                    if (member is Microsoft.Graph.Models.User user)
                    {
                        members.Add(user.UserPrincipalName ?? user.Id ?? "");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log($"Azure AD get group members: {groupName}", ex);
            throw;
        }

        return members;
    }
}
