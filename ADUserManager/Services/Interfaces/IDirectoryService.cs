using ActiveManager.Services.Models;

namespace ActiveManager.Services.Interfaces;

/// <summary>
/// Base interface for all directory service implementations.
/// Provides common properties and connection testing capabilities.
/// </summary>
public interface IDirectoryService : IDisposable
{
    /// <summary>
    /// Gets the domain name or tenant identifier for the directory.
    /// </summary>
    string DomainName { get; }

    /// <summary>
    /// Gets the type of directory service (On-Premises AD or Azure AD).
    /// </summary>
    DirectoryType Type { get; }

    /// <summary>
    /// Tests the connection to the directory service.
    /// </summary>
    /// <returns>True if connection is successful, false otherwise.</returns>
    Task<bool> TestConnectionAsync();
}
