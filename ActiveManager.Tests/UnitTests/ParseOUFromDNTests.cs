using ActiveManager.Services;

namespace ActiveManager.Tests.UnitTests;

public class ParseOUFromDNTests
{
    [Fact]
    public void StandardDN_ReturnsOU()
    {
        var result = TerminationService.ParseOUFromDN("CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt");
        Assert.Equal("OU=IT,DC=company,DC=lt", result);
    }

    [Fact]
    public void NestedOUs_ReturnsFullOUPath()
    {
        var result = TerminationService.ParseOUFromDN("CN=User,OU=Sub,OU=Main,DC=company,DC=lt");
        Assert.Equal("OU=Sub,OU=Main,DC=company,DC=lt", result);
    }

    [Fact]
    public void NullInput_ReturnsNull()
    {
        var result = TerminationService.ParseOUFromDN(null!);
        Assert.Null(result);
    }

    [Fact]
    public void EmptyString_ReturnsNull()
    {
        var result = TerminationService.ParseOUFromDN("");
        Assert.Null(result);
    }

    [Fact]
    public void NoComma_ReturnsNull()
    {
        var result = TerminationService.ParseOUFromDN("CN=User");
        Assert.Null(result);
    }

    [Fact]
    public void DCOnly_ReturnsDC()
    {
        var result = TerminationService.ParseOUFromDN("CN=User,DC=company,DC=lt");
        Assert.Equal("DC=company,DC=lt", result);
    }

    [Fact]
    public void ContainerDN_ReturnsCNContainer()
    {
        var result = TerminationService.ParseOUFromDN("CN=User,CN=Users,DC=company,DC=lt");
        Assert.Equal("CN=Users,DC=company,DC=lt", result);
    }

    [Fact]
    public void DeeplyNestedOU_ReturnsFullPath()
    {
        var dn = "CN=Test User,OU=Level3,OU=Level2,OU=Level1,DC=corp,DC=example,DC=com";
        var result = TerminationService.ParseOUFromDN(dn);
        Assert.Equal("OU=Level3,OU=Level2,OU=Level1,DC=corp,DC=example,DC=com", result);
    }

    [Fact]
    public void BuildDefaultUsersContainerDn_ReturnsExpectedDn()
    {
        var result = TerminationService.BuildDefaultUsersContainerDn("DC=company,DC=lt");

        Assert.Equal("CN=Users,DC=company,DC=lt", result);
    }

    [Fact]
    public void BuildUserPlacementTargets_AddsUsersContainerAndSorts()
    {
        var result = TerminationService.BuildUserPlacementTargets(
            new[]
            {
                "OU=Sales,DC=company,DC=lt",
                "OU=IT,DC=company,DC=lt"
            },
            "DC=company,DC=lt");

        Assert.Equal(new[]
        {
            "CN=Users,DC=company,DC=lt",
            "OU=IT,DC=company,DC=lt",
            "OU=Sales,DC=company,DC=lt"
        }, result);
    }

    [Fact]
    public void BuildUserPlacementTargets_DoesNotDuplicateUsersContainer()
    {
        var result = TerminationService.BuildUserPlacementTargets(
            new[]
            {
                "CN=Users,DC=company,DC=lt",
                "OU=IT,DC=company,DC=lt"
            },
            "DC=company,DC=lt");

        Assert.Equal(2, result.Count);
        Assert.Equal(1, result.Count(dn => dn == "CN=Users,DC=company,DC=lt"));
    }
}
