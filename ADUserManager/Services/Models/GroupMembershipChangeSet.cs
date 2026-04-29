namespace ActiveManager.Services.Models;

public class GroupMembershipChangeSet
{
    public List<string> GroupsToAdd { get; } = new();
    public List<string> GroupsToRemove { get; } = new();
}