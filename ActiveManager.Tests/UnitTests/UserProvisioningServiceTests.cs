using ActiveManager.Services;
using ActiveManager.Services.Models;
using ActiveManager.Views.Dialogs;

namespace ActiveManager.Tests.UnitTests;

public class UserProvisioningServiceTests
{
    [Fact]
    public void ValidateRequest_MissingFirstName_ReturnsError()
    {
        var request = CreateValidRequest();
        request.FirstName = "";

        var result = UserProvisioningService.ValidateRequest(request);

        Assert.Equal("Enter a first name.", result);
    }

    [Fact]
    public void ValidateRequest_InvalidEmail_ReturnsError()
    {
        var request = CreateValidRequest();
        request.Email = "not-an-email";

        var result = UserProvisioningService.ValidateRequest(request);

        Assert.Equal("Enter a valid email address.", result);
    }

    [Fact]
    public void ValidateRequest_ValidRequest_ReturnsNull()
    {
        var result = UserProvisioningService.ValidateRequest(CreateValidRequest());

        Assert.Null(result);
    }

    [Fact]
    public void BuildDisplayName_TrimsAndConcatenatesNames()
    {
        var result = UserProvisioningService.BuildDisplayName("  Jonas ", " Jonaitis  ");

        Assert.Equal("Jonas Jonaitis", result);
    }

    [Fact]
    public void BuildUniquenessError_BothDuplicates_ReturnsCombinedMessage()
    {
        var result = UserProvisioningService.BuildUniquenessError(samExists: true, emailExists: true);

        Assert.Equal("The username and email address are already in use.", result);
    }

    [Fact]
    public void GenerateSecurePassword_ReturnsExpectedLengthAndCharacterMix()
    {
        var password = UserProvisioningService.GenerateSecurePassword();

        Assert.Equal(16, password.Length);
        Assert.Contains(password, char.IsUpper);
        Assert.Contains(password, char.IsLower);
        Assert.Contains(password, char.IsDigit);
        Assert.Contains(password, c => "!@#$%^&*".Contains(c));
    }

    [Fact]
    public void ValidateUpdateRequest_ValidRequest_ReturnsNull()
    {
        var result = UserService.ValidateUpdateRequest(CreateValidUpdateRequest());

        Assert.Null(result);
    }

    [Fact]
    public void ValidateUpdateRequest_MissingOriginalSamAccountName_ReturnsError()
    {
        var request = CreateValidUpdateRequest();
        request.OriginalSamAccountName = "";

        var result = UserService.ValidateUpdateRequest(request);

        Assert.Equal("Failed to determine which user is being edited.", result);
    }

    [Theory]
    [InlineData("user@company.lt", "user@company.lt", false)]
    [InlineData("user@company.lt", "USER@company.lt", false)]
    [InlineData("user@company.lt", "new.user@company.lt", true)]
    public void HasEmailChanged_DetectsChangedValues(string currentEmail, string requestedEmail, bool expected)
    {
        var result = UserService.HasEmailChanged(currentEmail, requestedEmail);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateGroupMembershipChanges_ReturnsAddsAndRemoves()
    {
        var result = UserService.CalculateGroupMembershipChanges(
            new[]
            {
                "CN=Accounting,OU=Groups,DC=company,DC=lt",
                "CN=HR,OU=Groups,DC=company,DC=lt"
            },
            new[]
            {
                "CN=HR,OU=Groups,DC=company,DC=lt",
                "CN=IT,OU=Groups,DC=company,DC=lt"
            });

        Assert.Equal(new[] { "CN=IT,OU=Groups,DC=company,DC=lt" }, result.GroupsToAdd);
        Assert.Equal(new[] { "CN=Accounting,OU=Groups,DC=company,DC=lt" }, result.GroupsToRemove);
    }

    [Fact]
    public void BuildClipboardText_ContainsUsernameAndPassword()
    {
        var result = new CreateUserResult
        {
            User = new ADUserModel { SamAccountName = "j.jonaitis" },
            GeneratedPassword = "Secret123!"
        };

        var clipboardText = UserCredentialsDialog.BuildClipboardText(result);

        Assert.Contains("Username: j.jonaitis", clipboardText);
        Assert.Contains("Password: Secret123!", clipboardText);
    }

    [Fact]
    public void BuildPasswordResetClipboardText_ContainsUsernameAndPassword()
    {
        var credentials = new PasswordResetCredentials
        {
            SamAccountName = "j.jonaitis",
            Password = "Secret123!"
        };

        var clipboardText = PasswordResetCredentialsDialog.BuildClipboardText(credentials);

        Assert.Contains("Username: j.jonaitis", clipboardText);
        Assert.Contains("Password: Secret123!", clipboardText);
    }

    private static CreateUserRequest CreateValidRequest()
    {
        return new CreateUserRequest
        {
            FirstName = "Jonas",
            LastName = "Jonaitis",
            SamAccountName = "j.jonaitis",
            Email = "jonas.jonaitis@company.lt",
            TargetOU = "OU=IT,DC=company,DC=lt"
        };
    }

    private static UpdateUserRequest CreateValidUpdateRequest()
    {
        return new UpdateUserRequest
        {
            OriginalSamAccountName = "j.jonaitis",
            FirstName = "Jonas",
            LastName = "Jonaitis",
            Email = "jonas.jonaitis@company.lt",
            TargetOU = "OU=IT,DC=company,DC=lt",
            SelectedGroups = new List<string>()
        };
    }
}
