namespace ActiveManager.Services.Models;

public class UpdateUserRequest
{
    public string OriginalSamAccountName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string TargetOU { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SelectedGroups { get; set; } = new();
    public bool Enabled { get; set; } = true;
}
