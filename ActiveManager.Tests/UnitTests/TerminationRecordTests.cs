using ActiveManager.Services.Models;

namespace ActiveManager.Tests.UnitTests;

public class TerminationRecordTests
{
    [Fact]
    public void DefaultTerminatedAt_IsApproximatelyNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var record = new TerminationRecord();
        var after = DateTime.Now.AddSeconds(1);

        Assert.InRange(record.TerminatedAt, before, after);
    }

    [Fact]
    public void DefaultPerformedBy_IsCurrentUser()
    {
        var record = new TerminationRecord();
        Assert.Equal(Environment.UserName, record.PerformedBy);
    }

    [Fact]
    public void AllBoolFlags_DefaultToFalse()
    {
        var record = new TerminationRecord();

        Assert.False(record.AccountDisabled);
        Assert.False(record.MovedToDisabledOU);
        Assert.False(record.PasswordChanged);
        Assert.False(record.RemovedFromGroups);
        Assert.False(record.AccountExpirationSet);
        Assert.False(record.DataExported);
        Assert.False(record.RolledBack);
    }

    [Fact]
    public void NullableFields_DefaultToNull()
    {
        var record = new TerminationRecord();

        Assert.Null(record.TargetOU);
        Assert.Null(record.OriginalOU);
        Assert.Null(record.ExpirationDate);
        Assert.Null(record.ExportPath);
        Assert.Null(record.RolledBackAt);
        Assert.Null(record.RolledBackBy);
    }

    [Fact]
    public void GroupMemberships_DefaultsToEmptyList()
    {
        var record = new TerminationRecord();
        Assert.NotNull(record.GroupMemberships);
        Assert.Empty(record.GroupMemberships);
    }

    [Fact]
    public void StepResults_DefaultsToEmptyList()
    {
        var record = new TerminationRecord();
        Assert.NotNull(record.StepResults);
        Assert.Empty(record.StepResults);
    }

    [Fact]
    public void StringFields_DefaultToEmpty()
    {
        var record = new TerminationRecord();

        Assert.Equal(string.Empty, record.SamAccountName);
        Assert.Equal(string.Empty, record.DisplayName);
        Assert.Equal(string.Empty, record.DistinguishedName);
    }

    [Fact]
    public void GroupMembershipRecord_DefaultsToEmpty()
    {
        var group = new GroupMembershipRecord();
        Assert.Equal(string.Empty, group.GroupName);
        Assert.Equal(string.Empty, group.GroupDN);
    }

    [Fact]
    public void TerminationStepResult_DefaultValues()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var step = new TerminationStepResult();
        var after = DateTime.Now.AddSeconds(1);

        Assert.Equal(string.Empty, step.StepName);
        Assert.False(step.Success);
        Assert.Null(step.ErrorMessage);
        Assert.InRange(step.ExecutedAt, before, after);
    }
}
