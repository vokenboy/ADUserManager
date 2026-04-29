using ActiveManager.Services.Models;

namespace ActiveManager.Services;

/// <summary>
/// Memento pattern - Originator interface.
/// Defines the ability to create state snapshots and restore from them.
/// </summary>
public interface IUserOriginator
{
    /// <summary>
    /// Captures the current AD state of the user into an immutable memento.
    /// Should be called BEFORE any destructive operations.
    /// </summary>
    UserMemento CreateMemento(ADUserModel user);

    /// <summary>
    /// Restores a user's AD state from a previously captured memento.
    /// Re-enables account, moves back to original OU, restores group memberships,
    /// and clears account expiration.
    /// </summary>
    void RestoreFromMemento(UserMemento memento);
}
