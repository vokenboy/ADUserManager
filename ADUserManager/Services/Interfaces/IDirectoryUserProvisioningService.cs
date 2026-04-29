using ActiveManager.Services.Models;

namespace ActiveManager.Services.Interfaces;

/// <summary>
/// Interface for provisioning new directory users.
/// </summary>
public interface IDirectoryUserProvisioningService : IDirectoryService
{
    /// <summary>
    /// Creates a new user account in the directory.
    /// </summary>
    Task<CreateUserResult> CreateUserAsync(CreateUserRequest request);
}
