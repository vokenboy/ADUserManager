namespace ADUserManager.Services.Models;

public class ADGroupModel
{
    public string Name { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string GroupScope { get; set; } = string.Empty;
    public string GroupCategory { get; set; } = string.Empty;
    public int MemberCount { get; set; }
}
