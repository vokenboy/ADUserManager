using ActiveManager.Services.Models;

namespace ActiveManager.Services.Interfaces;

/// <summary>
/// Interface for user search and retrieval operations across directory services.
/// </summary>
public interface IDirectoryUserService : IDirectoryService
{
    /// <summary>
    /// Searches for users matching the specified filter.
    /// </summary>
    /// <param name="filter">Search filter (name, email, username, etc.)</param>
    /// <returns>List of users matching the filter.</returns>
    Task<List<ADUserModel>> SearchUsersAsync(string filter);

    /// <summary>
    /// Retrieves detailed information about a specific user.
    /// </summary>
    /// <param name="identifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <returns>User details or null if not found.</returns>
    Task<ADUserModel?> GetUserAsync(string identifier);
}
