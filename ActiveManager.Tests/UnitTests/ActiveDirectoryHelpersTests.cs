using ActiveManager.Services;

namespace ActiveManager.Tests.UnitTests;

public class ActiveDirectoryHelpersTests
{
    // === EscapeLdapFilter tests ===

    [Fact]
    public void EscapeLdapFilter_BackslashEscaped()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter(@"test\value");
        Assert.Equal(@"test\5cvalue", result);
    }

    [Fact]
    public void EscapeLdapFilter_AsteriskEscaped()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter("test*value");
        Assert.Equal(@"test\2avalue", result);
    }

    [Fact]
    public void EscapeLdapFilter_ParenthesesEscaped()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter("(test)");
        Assert.Equal(@"\28test\29", result);
    }

    [Fact]
    public void EscapeLdapFilter_NullCharEscaped()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter("test\0value");
        Assert.Equal(@"test\00value", result);
    }

    [Fact]
    public void EscapeLdapFilter_CombinedSpecialChars()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter(@"a\b*c(d)e");
        Assert.Equal(@"a\5cb\2ac\28d\29e", result);
    }

    [Fact]
    public void EscapeLdapFilter_NormalString_PassesThrough()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter("Jonas Jonaitis");
        Assert.Equal("Jonas Jonaitis", result);
    }

    [Fact]
    public void EscapeLdapFilter_EmptyString_ReturnsEmpty()
    {
        var result = ActiveDirectoryBase.EscapeLdapFilter("");
        Assert.Equal("", result);
    }

    // === FileTimeToDateTime tests ===

    [Fact]
    public void FileTimeToDateTime_Zero_ReturnsNull()
    {
        var result = ActiveDirectoryBase.FileTimeToDateTime(0);
        Assert.Null(result);
    }

    [Fact]
    public void FileTimeToDateTime_Negative_ReturnsNull()
    {
        var result = ActiveDirectoryBase.FileTimeToDateTime(-1);
        Assert.Null(result);
    }

    [Fact]
    public void FileTimeToDateTime_MaxValue_ReturnsNull()
    {
        var result = ActiveDirectoryBase.FileTimeToDateTime(long.MaxValue);
        Assert.Null(result);
    }

    [Fact]
    public void FileTimeToDateTime_ValidFileTime_ReturnsCorrectDate()
    {
        // 2025-01-01 00:00:00 UTC as FILETIME
        var date = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var fileTime = date.ToFileTime();

        var result = ActiveDirectoryBase.FileTimeToDateTime(fileTime);

        Assert.NotNull(result);
        Assert.Equal(date.ToLocalTime(), result.Value);
    }
}
