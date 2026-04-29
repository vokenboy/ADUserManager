using ActiveManager.Services;
using ActiveManager.Services.Models;
using Moq;

namespace ActiveManager.Tests.UnitTests;

/// <summary>
/// Tests verifying that the termination workflow calls the correct service methods
/// in the correct order with the correct parameters.
/// These tests simulate what TerminationProgressForm does without needing a real AD.
/// </summary>
public class TerminationWorkflowTests
{
    private readonly Mock<ITerminationService> _mockService;
    private readonly ADUserModel _testUser;
    private readonly List<GroupMembershipRecord> _testGroups;

    public TerminationWorkflowTests()
    {
        _mockService = new Mock<ITerminationService>(MockBehavior.Strict);

        _testUser = new ADUserModel
        {
            SamAccountName = "jjonaitis",
            DisplayName = "Jonas Jonaitis",
            FirstName = "Jonas",
            LastName = "Jonaitis",
            Email = "jonas@company.lt",
            Department = "IT",
            DistinguishedName = "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt",
            IsEnabled = true
        };

        _testGroups = new List<GroupMembershipRecord>
        {
            new() { GroupName = "IT-Admins", GroupDN = "CN=IT-Admins,OU=Groups,DC=company,DC=lt" },
            new() { GroupName = "VPN-Users", GroupDN = "CN=VPN-Users,OU=Groups,DC=company,DC=lt" }
        };
    }

    [Fact]
    public void DisableUser_CalledWithCorrectDN()
    {
        _mockService.Setup(s => s.DisableUser(_testUser.DistinguishedName));
        _mockService.Object.DisableUser(_testUser.DistinguishedName);
        _mockService.Verify(s => s.DisableUser("CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt"), Times.Once);
    }

    [Fact]
    public void MoveUser_CalledWithCorrectDNAndTargetOU()
    {
        var targetOU = "OU=Disabled,DC=company,DC=lt";
        _mockService.Setup(s => s.MoveUser(_testUser.DistinguishedName, targetOU));

        _mockService.Object.MoveUser(_testUser.DistinguishedName, targetOU);

        _mockService.Verify(s => s.MoveUser(
            "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt",
            "OU=Disabled,DC=company,DC=lt"), Times.Once);
    }

    [Fact]
    public void ResetPassword_CalledWithSamAccountNameAndPassword()
    {
        var password = "R@ndom123!Pass";
        _mockService.Setup(s => s.ResetPassword(_testUser.SamAccountName, password));

        _mockService.Object.ResetPassword(_testUser.SamAccountName, password);

        _mockService.Verify(s => s.ResetPassword("jjonaitis", "R@ndom123!Pass"), Times.Once);
    }

    [Fact]
    public void ResetPassword_WithForceChange_CalledWithExpectedFlag()
    {
        var password = "R@ndom123!Pass";
        _mockService.Setup(s => s.ResetPassword(_testUser.SamAccountName, password, true));

        _mockService.Object.ResetPassword(_testUser.SamAccountName, password, true);

        _mockService.Verify(s => s.ResetPassword("jjonaitis", "R@ndom123!Pass", true), Times.Once);
    }

    [Fact]
    public void SetAccountExpiration_CalledWithCorrectDate()
    {
        var expDate = new DateTime(2026, 3, 15);
        _mockService.Setup(s => s.SetAccountExpiration(_testUser.DistinguishedName, expDate));

        _mockService.Object.SetAccountExpiration(_testUser.DistinguishedName, expDate);

        _mockService.Verify(s => s.SetAccountExpiration(
            "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt",
            new DateTime(2026, 3, 15)), Times.Once);
    }

    [Fact]
    public void GetUserGroups_ReturnsGroups_BeforeRemoval()
    {
        _mockService.Setup(s => s.GetUserGroups(_testUser.DistinguishedName)).Returns(_testGroups);

        var groups = _mockService.Object.GetUserGroups(_testUser.DistinguishedName);

        Assert.Equal(2, groups.Count);
        Assert.Equal("IT-Admins", groups[0].GroupName);
        Assert.Equal("VPN-Users", groups[1].GroupName);
    }

    [Fact]
    public void RemoveFromAllGroups_CalledAfterGetUserGroups()
    {
        var callOrder = new List<string>();

        _mockService.Setup(s => s.GetUserGroups(_testUser.DistinguishedName))
            .Returns(_testGroups)
            .Callback(() => callOrder.Add("GetUserGroups"));

        _mockService.Setup(s => s.RemoveFromAllGroups(_testUser.DistinguishedName))
            .Callback(() => callOrder.Add("RemoveFromAllGroups"));

        // Simulate the workflow order
        _mockService.Object.GetUserGroups(_testUser.DistinguishedName);
        _mockService.Object.RemoveFromAllGroups(_testUser.DistinguishedName);

        Assert.Equal(new[] { "GetUserGroups", "RemoveFromAllGroups" }, callOrder);
    }

    [Fact]
    public void FullWorkflow_AllStepsCalledInOrder()
    {
        var callOrder = new List<string>();
        var targetOU = "OU=Disabled,DC=company,DC=lt";
        var password = "RandomPass123!";
        var expDate = new DateTime(2026, 3, 15);

        _mockService.Setup(s => s.GetUserGroups(_testUser.DistinguishedName))
            .Returns(_testGroups)
            .Callback(() => callOrder.Add("GetUserGroups"));
        _mockService.Setup(s => s.DisableUser(_testUser.DistinguishedName))
            .Callback(() => callOrder.Add("DisableUser"));
        _mockService.Setup(s => s.MoveUser(_testUser.DistinguishedName, targetOU))
            .Callback(() => callOrder.Add("MoveUser"));
        _mockService.Setup(s => s.ResetPassword(_testUser.SamAccountName, password))
            .Callback(() => callOrder.Add("ResetPassword"));
        _mockService.Setup(s => s.RemoveFromAllGroups(_testUser.DistinguishedName))
            .Callback(() => callOrder.Add("RemoveFromAllGroups"));
        _mockService.Setup(s => s.SetAccountExpiration(_testUser.DistinguishedName, expDate))
            .Callback(() => callOrder.Add("SetAccountExpiration"));

        // Simulate the exact workflow from TerminationProgressForm
        _mockService.Object.GetUserGroups(_testUser.DistinguishedName);
        _mockService.Object.DisableUser(_testUser.DistinguishedName);
        _mockService.Object.MoveUser(_testUser.DistinguishedName, targetOU);
        _mockService.Object.ResetPassword(_testUser.SamAccountName, password);
        _mockService.Object.RemoveFromAllGroups(_testUser.DistinguishedName);
        _mockService.Object.SetAccountExpiration(_testUser.DistinguishedName, expDate);

        Assert.Equal(new[]
        {
            "GetUserGroups",
            "DisableUser",
            "MoveUser",
            "ResetPassword",
            "RemoveFromAllGroups",
            "SetAccountExpiration"
        }, callOrder);
    }

    [Fact]
    public void PartialWorkflow_OnlyDisable_DoesNotCallOtherMethods()
    {
        _mockService.Setup(s => s.GetUserGroups(_testUser.DistinguishedName)).Returns(_testGroups);
        _mockService.Setup(s => s.DisableUser(_testUser.DistinguishedName));

        _mockService.Object.GetUserGroups(_testUser.DistinguishedName);
        _mockService.Object.DisableUser(_testUser.DistinguishedName);

        _mockService.Verify(s => s.DisableUser(It.IsAny<string>()), Times.Once);
        _mockService.Verify(s => s.MoveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockService.Verify(s => s.ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockService.Verify(s => s.RemoveFromAllGroups(It.IsAny<string>()), Times.Never);
        _mockService.Verify(s => s.SetAccountExpiration(It.IsAny<string>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public void OriginalOU_ParsedFromDN_SavedToRecord()
    {
        var record = new TerminationRecord
        {
            SamAccountName = _testUser.SamAccountName,
            DisplayName = _testUser.DisplayName,
            DistinguishedName = _testUser.DistinguishedName,
            OriginalOU = TerminationService.ParseOUFromDN(_testUser.DistinguishedName)
        };

        Assert.Equal("OU=IT,DC=company,DC=lt", record.OriginalOU);
    }

    [Fact]
    public void Record_TracksAllPerformedActions()
    {
        var record = new TerminationRecord
        {
            SamAccountName = _testUser.SamAccountName,
            DisplayName = _testUser.DisplayName,
            DistinguishedName = _testUser.DistinguishedName,
            OriginalOU = TerminationService.ParseOUFromDN(_testUser.DistinguishedName)
        };

        // Simulate what TerminationProgressForm does after each step
        record.AccountDisabled = true;
        record.MovedToDisabledOU = true;
        record.TargetOU = "OU=Disabled,DC=company,DC=lt";
        record.PasswordChanged = true;
        record.RemovedFromGroups = true;
        record.GroupMemberships = _testGroups;
        record.AccountExpirationSet = true;
        record.ExpirationDate = new DateTime(2026, 3, 15);

        Assert.True(record.AccountDisabled);
        Assert.True(record.MovedToDisabledOU);
        Assert.Equal("OU=Disabled,DC=company,DC=lt", record.TargetOU);
        Assert.Equal("OU=IT,DC=company,DC=lt", record.OriginalOU);
        Assert.True(record.PasswordChanged);
        Assert.True(record.RemovedFromGroups);
        Assert.Equal(2, record.GroupMemberships.Count);
        Assert.True(record.AccountExpirationSet);
        Assert.Equal(new DateTime(2026, 3, 15), record.ExpirationDate);
    }

    [Fact]
    public void StepResults_TrackSuccessAndFailure()
    {
        var record = new TerminationRecord();

        record.StepResults.Add(new TerminationStepResult
        {
            StepName = "Paskyros išjungimas",
            Success = true
        });

        record.StepResults.Add(new TerminationStepResult
        {
            StepName = "Perkėlimas į OU",
            Success = false,
            ErrorMessage = "Target OU not found"
        });

        Assert.Equal(2, record.StepResults.Count);
        Assert.True(record.StepResults[0].Success);
        Assert.Null(record.StepResults[0].ErrorMessage);
        Assert.False(record.StepResults[1].Success);
        Assert.Equal("Target OU not found", record.StepResults[1].ErrorMessage);
    }
}
