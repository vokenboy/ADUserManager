using ActiveManager.Services.Models;

namespace ActiveManager.Services.Interfaces;

/// <summary>
/// Interface for user account management and termination operations across directory services.
/// Implements the Memento pattern via IUserOriginator for state capture and rollback.
/// </summary>
public interface IDirectoryTerminationService : IDirectoryService, IUserOriginator
{
    /// <summary>
    /// Disables a user account, preventing sign-in.
    /// </summary>
    /// <param name="identifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> DisableUserAsync(string identifier);

    /// <summary>
    /// Enables a user account, allowing sign-in.
    /// </summary>
    /// <param name="identifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> EnableUserAsync(string identifier);

    /// <summary>
    /// Resets a user's password to a new value.
    /// </summary>
    /// <param name="identifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <param name="newPassword">The new password to set</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> ResetPasswordAsync(string identifier, string newPassword);

    /// <summary>
    /// Adds a user to a specified group.
    /// </summary>
    /// <param name="userIdentifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <param name="groupName">Group name or identifier</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> AddToGroupAsync(string userIdentifier, string groupName);

    /// <summary>
    /// Removes a user from a specified group.
    /// </summary>
    /// <param name="userIdentifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <param name="groupName">Group name or identifier</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> RemoveFromGroupAsync(string userIdentifier, string groupName);

    /// <summary>
    /// Removes a user from all groups they are a member of.
    /// </summary>
    /// <param name="identifier">User identifier (SAMAccountName, UPN, or ObjectId)</param>
    /// <returns>True if successful, false otherwise.</returns>
    Task<bool> RemoveFromAllGroupsAsync(string identifier);
}
