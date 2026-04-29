using ActiveManager.Services.Models;

namespace ActiveManager.Services.Interfaces;

/// <summary>
/// Interface for group search and membership operations across directory services.
/// </summary>
public interface IDirectoryGroupService : IDirectoryService
{
    /// <summary>
    /// Searches for groups matching the specified filter.
    /// </summary>
    /// <param name="filter">Search filter (group name)</param>
    /// <returns>List of groups matching the filter.</returns>
    Task<List<ADGroupModel>> SearchGroupsAsync(string filter);

    /// <summary>
    /// Retrieves all members of a specified group.
    /// </summary>
    /// <param name="groupName">Group name or identifier</param>
    /// <returns>List of member identifiers (SAMAccountName or UPN).</returns>
    Task<List<string>> GetGroupMembersAsync(string groupName);
}
