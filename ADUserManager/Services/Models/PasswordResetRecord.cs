namespace ActiveManager.Services.Models;

public class PasswordResetRecord
{
    public int Id { get; set; }
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DistinguishedName { get; set; } = string.Empty;
    public DateTime ResetAt { get; set; } = DateTime.Now;
    public string PerformedBy { get; set; } = Environment.UserName;
    public bool ForceChangeAtNextSignIn { get; set; }
}

public class PasswordResetCredentials
{
    public string SamAccountName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool ForceChangeAtNextSignIn { get; set; }
}
