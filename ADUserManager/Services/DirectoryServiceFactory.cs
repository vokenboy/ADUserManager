using ActiveManager.Services.Interfaces;
using ActiveManager.Services.Models;
using ActiveManager.Services.AzureAD;

namespace ActiveManager.Services;

/// <summary>
/// Factory for creating directory service instances based on configuration.
/// Implements the Factory pattern to abstract directory service creation.
/// </summary>
public class DirectoryServiceFactory
{
    private readonly AppSettings _settings;

    public DirectoryServiceFactory()
    {
        _settings = AppSettings.Instance;
    }

    /// <summary>
    /// Creates a user service instance for the configured directory type.
    /// </summary>
    public IDirectoryUserService CreateUserService()
    {
        return _settings.Directory.DirectoryType switch
        {
            DirectoryType.AzureAD => new AzureADUserService(_settings.Directory.AzureAD!),
            DirectoryType.OnPremisesAD => new UserService(),
            _ => throw new NotSupportedException($"Directory type {_settings.Directory.DirectoryType} not supported")
        };
    }

    /// <summary>
    /// Creates a group service instance for the configured directory type.
    /// </summary>
    public IDirectoryGroupService CreateGroupService()
    {
        return _settings.Directory.DirectoryType switch
        {
            DirectoryType.AzureAD => new AzureADGroupService(_settings.Directory.AzureAD!),
            DirectoryType.OnPremisesAD => new GroupService(),
            _ => throw new NotSupportedException($"Directory type {_settings.Directory.DirectoryType} not supported")
        };
    }

    /// <summary>
    /// Creates a termination service instance for the configured directory type.
    /// </summary>
    public IDirectoryTerminationService CreateTerminationService()
    {
        return _settings.Directory.DirectoryType switch
        {
            DirectoryType.AzureAD => new AzureADTerminationService(_settings.Directory.AzureAD!),
            DirectoryType.OnPremisesAD => new TerminationService(),
            _ => throw new NotSupportedException($"Directory type {_settings.Directory.DirectoryType} not supported")
        };
    }
}
