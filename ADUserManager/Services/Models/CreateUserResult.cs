namespace ActiveManager.Services.Models;

public class CreateUserResult
{
    public bool IsSuccess { get; set; }
    public bool UserCreated { get; set; }
    public ADUserModel? User { get; set; }
    public string GeneratedPassword { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = new();
}
