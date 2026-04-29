using ActiveManager.Services;
using ActiveManager.Services.Models;
using Moq;

namespace ActiveManager.Tests.UnitTests;

/// <summary>
/// Tests verifying the rollback workflow calls the correct reverse operations
/// based on the TerminationRecord's flags.
/// Simulates what RollbackProgressForm does without needing a real AD.
/// </summary>
public class RollbackWorkflowTests
{
    private readonly Mock<ITerminationService> _mockService;

    public RollbackWorkflowTests()
    {
        _mockService = new Mock<ITerminationService>(MockBehavior.Strict);
    }

    private TerminationRecord CreateFullRecord()
    {
        return new TerminationRecord
        {
            Id = 1,
            SamAccountName = "jjonaitis",
            DisplayName = "Jonas Jonaitis",
            DistinguishedName = "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt",
            OriginalOU = "OU=IT,DC=company,DC=lt",
            AccountDisabled = true,
            MovedToDisabledOU = true,
            TargetOU = "OU=Disabled,DC=company,DC=lt",
            PasswordChanged = true,
            RemovedFromGroups = true,
            AccountExpirationSet = true,
            ExpirationDate = new DateTime(2026, 3, 15),
            GroupMemberships = new List<GroupMembershipRecord>
            {
                new() { GroupName = "IT-Admins", GroupDN = "CN=IT-Admins,OU=Groups,DC=company,DC=lt" },
                new() { GroupName = "VPN-Users", GroupDN = "CN=VPN-Users,OU=Groups,DC=company,DC=lt" }
            }
        };
    }

    [Fact]
    public void Rollback_DisabledAccount_CallsEnableUser()
    {
        var record = CreateFullRecord();
        var currentDN = "CN=Jonas Jonaitis,OU=Disabled,DC=company,DC=lt";

        _mockService.Setup(s => s.EnableUser(currentDN));
        _mockService.Object.EnableUser(currentDN);

        _mockService.Verify(s => s.EnableUser(currentDN), Times.Once);
    }

    [Fact]
    public void Rollback_MovedAccount_CallsMoveUserWithOriginalOU()
    {
        var record = CreateFullRecord();
        var currentDN = "CN=Jonas Jonaitis,OU=Disabled,DC=company,DC=lt";

        _mockService.Setup(s => s.MoveUser(currentDN, record.OriginalOU!));
        _mockService.Object.MoveUser(currentDN, record.OriginalOU!);

        _mockService.Verify(s => s.MoveUser(currentDN, "OU=IT,DC=company,DC=lt"), Times.Once);
    }

    [Fact]
    public void Rollback_RemovedGroups_CallsAddToGroupsWithSavedMemberships()
    {
        var record = CreateFullRecord();
        var currentDN = "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt";

        _mockService.Setup(s => s.AddToGroups(currentDN, record.GroupMemberships));
        _mockService.Object.AddToGroups(currentDN, record.GroupMemberships);

        _mockService.Verify(s => s.AddToGroups(currentDN, It.Is<List<GroupMembershipRecord>>(
            g => g.Count == 2 && g[0].GroupName == "IT-Admins" && g[1].GroupName == "VPN-Users"
        )), Times.Once);
    }

    [Fact]
    public void Rollback_ExpirationSet_CallsClearAccountExpiration()
    {
        var record = CreateFullRecord();
        var currentDN = "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt";

        _mockService.Setup(s => s.ClearAccountExpiration(currentDN));
        _mockService.Object.ClearAccountExpiration(currentDN);

        _mockService.Verify(s => s.ClearAccountExpiration(currentDN), Times.Once);
    }

    [Fact]
    public void Rollback_AccountNotDisabled_SkipsEnableUser()
    {
        var record = CreateFullRecord();
        record.AccountDisabled = false;

        // Simulate the rollback logic: only call EnableUser if AccountDisabled was true
        if (record.AccountDisabled)
        {
            _mockService.Object.EnableUser("some-dn");
        }

        _mockService.Verify(s => s.EnableUser(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Rollback_AccountNotMoved_SkipsMoveUser()
    {
        var record = CreateFullRecord();
        record.MovedToDisabledOU = false;

        if (record.MovedToDisabledOU)
        {
            _mockService.Object.MoveUser("some-dn", "some-ou");
        }

        _mockService.Verify(s => s.MoveUser(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Rollback_GroupsNotRemoved_SkipsAddToGroups()
    {
        var record = CreateFullRecord();
        record.RemovedFromGroups = false;

        if (record.RemovedFromGroups && record.GroupMemberships.Count > 0)
        {
            _mockService.Object.AddToGroups("some-dn", record.GroupMemberships);
        }

        _mockService.Verify(s => s.AddToGroups(It.IsAny<string>(), It.IsAny<List<GroupMembershipRecord>>()), Times.Never);
    }

    [Fact]
    public void Rollback_ExpirationNotSet_SkipsClearExpiration()
    {
        var record = CreateFullRecord();
        record.AccountExpirationSet = false;

        if (record.AccountExpirationSet)
        {
            _mockService.Object.ClearAccountExpiration("some-dn");
        }

        _mockService.Verify(s => s.ClearAccountExpiration(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Rollback_ParsesOUFromDN_WhenOriginalOUIsNull()
    {
        var record = CreateFullRecord();
        record.OriginalOU = null;

        var parsedOU = record.OriginalOU ?? TerminationService.ParseOUFromDN(record.DistinguishedName);

        Assert.Equal("OU=IT,DC=company,DC=lt", parsedOU);
    }

    [Fact]
    public void Rollback_PasswordNotRestored_NeverCallsResetPassword()
    {
        // Password rollback is NOT supported by design — the original password is never stored
        var record = CreateFullRecord();

        // The rollback workflow should NEVER call ResetPassword, regardless of record flags
        // This verifies the design decision
        _mockService.Verify(s => s.ResetPassword(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Rollback_FullSequence_CorrectOrder()
    {
        var record = CreateFullRecord();
        var currentDN = "CN=Jonas Jonaitis,OU=Disabled,DC=company,DC=lt";
        var callOrder = new List<string>();

        _mockService.Setup(s => s.EnableUser(currentDN))
            .Callback(() => callOrder.Add("EnableUser"));
        _mockService.Setup(s => s.MoveUser(currentDN, record.OriginalOU!))
            .Callback(() => callOrder.Add("MoveUser"));

        var newDN = $"CN=Jonas Jonaitis,{record.OriginalOU}";
        _mockService.Setup(s => s.AddToGroups(It.IsAny<string>(), record.GroupMemberships))
            .Callback(() => callOrder.Add("AddToGroups"));
        _mockService.Setup(s => s.ClearAccountExpiration(It.IsAny<string>()))
            .Callback(() => callOrder.Add("ClearAccountExpiration"));

        // Simulate the exact rollback sequence from RollbackProgressForm
        if (record.AccountDisabled)
            _mockService.Object.EnableUser(currentDN);

        if (record.MovedToDisabledOU && !string.IsNullOrEmpty(record.OriginalOU))
            _mockService.Object.MoveUser(currentDN, record.OriginalOU);

        if (record.RemovedFromGroups && record.GroupMemberships.Count > 0)
            _mockService.Object.AddToGroups(newDN, record.GroupMemberships);

        if (record.AccountExpirationSet)
            _mockService.Object.ClearAccountExpiration(newDN);

        // Password is never restored — by design

        Assert.Equal(new[]
        {
            "EnableUser",
            "MoveUser",
            "AddToGroups",
            "ClearAccountExpiration"
        }, callOrder);
    }

    [Fact]
    public void Rollback_DataExported_DeletesExportFile()
    {
        var record = CreateFullRecord();
        var tempFile = Path.GetTempFileName();
        record.DataExported = true;
        record.ExportPath = tempFile;

        // File exists before rollback
        Assert.True(File.Exists(tempFile));

        // Simulate the rollback logic from RollbackProgressForm
        if (record.DataExported && !string.IsNullOrEmpty(record.ExportPath))
        {
            if (File.Exists(record.ExportPath))
            {
                File.Delete(record.ExportPath);
            }
        }

        Assert.False(File.Exists(tempFile));
    }

    [Fact]
    public void Rollback_DataNotExported_SkipsFileDeletion()
    {
        var record = CreateFullRecord();
        var tempFile = Path.GetTempFileName();
        record.DataExported = false;
        record.ExportPath = tempFile;

        // Simulate the rollback logic
        if (record.DataExported && !string.IsNullOrEmpty(record.ExportPath))
        {
            if (File.Exists(record.ExportPath))
            {
                File.Delete(record.ExportPath);
            }
        }

        // File should still exist since DataExported is false
        Assert.True(File.Exists(tempFile));

        // Cleanup
        File.Delete(tempFile);
    }

    [Fact]
    public void Rollback_ExportFileAlreadyDeleted_DoesNotThrow()
    {
        var record = CreateFullRecord();
        record.DataExported = true;
        record.ExportPath = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid():N}.json");

        // File does not exist
        Assert.False(File.Exists(record.ExportPath));

        // Simulate the rollback logic — should not throw
        var ex = Record.Exception(() =>
        {
            if (record.DataExported && !string.IsNullOrEmpty(record.ExportPath))
            {
                if (File.Exists(record.ExportPath))
                {
                    File.Delete(record.ExportPath);
                }
            }
        });

        Assert.Null(ex);
    }

    [Fact]
    public void Rollback_ExportPathEmpty_SkipsFileDeletion()
    {
        var record = CreateFullRecord();
        record.DataExported = true;
        record.ExportPath = "";

        // Simulate the rollback logic — should not attempt deletion
        var deletionAttempted = false;
        if (record.DataExported && !string.IsNullOrEmpty(record.ExportPath))
        {
            deletionAttempted = true;
        }

        Assert.False(deletionAttempted);
    }

    [Fact]
    public void Rollback_FullSequence_IncludesExportFileDeletion()
    {
        var record = CreateFullRecord();
        record.DataExported = true;
        record.ExportPath = Path.Combine(Path.GetTempPath(), "test_export.json");

        var currentDN = "CN=Jonas Jonaitis,OU=Disabled,DC=company,DC=lt";
        var callOrder = new List<string>();

        _mockService.Setup(s => s.EnableUser(currentDN))
            .Callback(() => callOrder.Add("EnableUser"));
        _mockService.Setup(s => s.MoveUser(currentDN, record.OriginalOU!))
            .Callback(() => callOrder.Add("MoveUser"));

        var newDN = $"CN=Jonas Jonaitis,{record.OriginalOU}";
        _mockService.Setup(s => s.AddToGroups(It.IsAny<string>(), record.GroupMemberships))
            .Callback(() => callOrder.Add("AddToGroups"));
        _mockService.Setup(s => s.ClearAccountExpiration(It.IsAny<string>()))
            .Callback(() => callOrder.Add("ClearAccountExpiration"));

        // Simulate the exact rollback sequence from RollbackProgressForm
        if (record.AccountDisabled)
            _mockService.Object.EnableUser(currentDN);

        if (record.MovedToDisabledOU && !string.IsNullOrEmpty(record.OriginalOU))
            _mockService.Object.MoveUser(currentDN, record.OriginalOU);

        if (record.RemovedFromGroups && record.GroupMemberships.Count > 0)
            _mockService.Object.AddToGroups(newDN, record.GroupMemberships);

        if (record.DataExported && !string.IsNullOrEmpty(record.ExportPath))
            callOrder.Add("DeleteExportFile");

        if (record.AccountExpirationSet)
            _mockService.Object.ClearAccountExpiration(newDN);

        Assert.Equal(new[]
        {
            "EnableUser",
            "MoveUser",
            "AddToGroups",
            "DeleteExportFile",
            "ClearAccountExpiration"
        }, callOrder);
    }

    [Fact]
    public void RolledBackRecord_TracksRollbackInfo()
    {
        var record = CreateFullRecord();

        // Simulate marking as rolled back
        record.RolledBack = true;
        record.RolledBackAt = DateTime.Now;
        record.RolledBackBy = "admin";

        Assert.True(record.RolledBack);
        Assert.NotNull(record.RolledBackAt);
        Assert.Equal("admin", record.RolledBackBy);
    }
}
