using ActiveManager.Services.Models;

namespace ActiveManager.Tests.UnitTests;

public class ADUserModelTests
{
    [Fact]
    public void StringFields_DefaultToEmpty()
    {
        var user = new ADUserModel();

        Assert.Equal(string.Empty, user.SamAccountName);
        Assert.Equal(string.Empty, user.DisplayName);
        Assert.Equal(string.Empty, user.FirstName);
        Assert.Equal(string.Empty, user.LastName);
        Assert.Equal(string.Empty, user.Email);
        Assert.Equal(string.Empty, user.Department);
        Assert.Equal(string.Empty, user.Title);
        Assert.Equal(string.Empty, user.Description);
        Assert.Equal(string.Empty, user.DistinguishedName);
        Assert.Equal(string.Empty, user.OrganizationalUnit);
    }

    [Fact]
    public void BoolFields_DefaultToFalse()
    {
        var user = new ADUserModel();

        Assert.False(user.IsEnabled);
        Assert.False(user.IsLockedOut);
    }

    [Fact]
    public void DateTimeFields_DefaultToNull()
    {
        var user = new ADUserModel();

        Assert.Null(user.PasswordLastSet);
        Assert.Null(user.LastLogon);
    }

    [Fact]
    public void PropertyRoundTrip_AllFieldsSetAndRead()
    {
        var now = DateTime.Now;
        var user = new ADUserModel
        {
            SamAccountName = "testuser",
            DisplayName = "Test User",
            FirstName = "Test",
            LastName = "User",
            Email = "test@company.lt",
            Department = "IT",
            Title = "Developer",
            Description = "Test description",
            DistinguishedName = "CN=Test User,OU=IT,DC=company,DC=lt",
            OrganizationalUnit = "OU=IT,DC=company,DC=lt",
            IsEnabled = true,
            IsLockedOut = true,
            PasswordLastSet = now,
            LastLogon = now
        };

        Assert.Equal("testuser", user.SamAccountName);
        Assert.Equal("Test User", user.DisplayName);
        Assert.Equal("Test", user.FirstName);
        Assert.Equal("User", user.LastName);
        Assert.Equal("test@company.lt", user.Email);
        Assert.Equal("IT", user.Department);
        Assert.Equal("Developer", user.Title);
        Assert.Equal("Test description", user.Description);
        Assert.Equal("CN=Test User,OU=IT,DC=company,DC=lt", user.DistinguishedName);
        Assert.Equal("OU=IT,DC=company,DC=lt", user.OrganizationalUnit);
        Assert.True(user.IsEnabled);
        Assert.True(user.IsLockedOut);
        Assert.Equal(now, user.PasswordLastSet);
        Assert.Equal(now, user.LastLogon);
    }
}
