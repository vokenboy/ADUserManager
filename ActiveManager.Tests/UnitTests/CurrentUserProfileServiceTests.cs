using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Tests.UnitTests;

public class CurrentUserProfileServiceTests
{
    [Fact]
    public void MatchesAnyIdentity_MatchesSamAccountName()
    {
        var user = new ADUserModel
        {
            SamAccountName = "j.smith",
            Email = "john.smith@company.lt"
        };

        var result = CurrentUserProfileService.MatchesAnyIdentity(user, new[] { "j.smith" });

        Assert.True(result);
    }

    [Fact]
    public void MatchesAnyIdentity_MatchesUserPrincipalNameOrEmailLocalPart()
    {
        var user = new ADUserModel
        {
            SamAccountName = "john.smith",
            UserPrincipalName = "john.smith@company.lt",
            Email = "john.smith@company.lt"
        };

        var result = CurrentUserProfileService.MatchesAnyIdentity(user, new[] { @"COMPANY\john.smith", "john.smith@company.lt" });

        Assert.True(result);
    }

    [Fact]
    public void MatchesAnyIdentity_ReturnsFalseForDifferentUser()
    {
        var user = new ADUserModel
        {
            SamAccountName = "a.johnson",
            UserPrincipalName = "alice.johnson@company.lt",
            Email = "alice.johnson@company.lt"
        };

        var result = CurrentUserProfileService.MatchesAnyIdentity(user, new[] { "john.smith", "john.smith@company.lt" });

        Assert.False(result);
    }
}
