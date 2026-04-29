using ActiveManager.Services;

namespace ActiveManager.Tests.UnitTests;

public class ErrorLoggerTests : IDisposable
{
    public ErrorLoggerTests()
    {
        // Clear state before each test since ErrorLogger is static
        ErrorLogger.Clear();
    }

    public void Dispose()
    {
        ErrorLogger.Clear();
    }

    [Fact]
    public void Log_WithStringMessage_AddsEntry()
    {
        ErrorLogger.Log("TestSource", "Test message");

        Assert.Single(ErrorLogger.LogEntries);
        Assert.Equal("TestSource", ErrorLogger.LogEntries[0].Source);
        Assert.Equal("Test message", ErrorLogger.LogEntries[0].Message);
        Assert.Null(ErrorLogger.LogEntries[0].StackTrace);
    }

    [Fact]
    public void Log_WithException_CapturesMessageAndStackTrace()
    {
        var ex = new InvalidOperationException("Something went wrong");
        ErrorLogger.Log("TestSource", ex);

        Assert.Single(ErrorLogger.LogEntries);
        Assert.Equal("TestSource", ErrorLogger.LogEntries[0].Source);
        Assert.Equal("Something went wrong", ErrorLogger.LogEntries[0].Message);
        Assert.NotNull(ErrorLogger.LogEntries[0].StackTrace);
        Assert.Contains("InvalidOperationException", ErrorLogger.LogEntries[0].StackTrace);
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        ErrorLogger.Log("Source1", "Message1");
        ErrorLogger.Log("Source2", "Message2");
        Assert.Equal(2, ErrorLogger.LogEntries.Count);

        ErrorLogger.Clear();

        Assert.Empty(ErrorLogger.LogEntries);
    }

    [Fact]
    public void EntryAdded_EventFires()
    {
        ErrorLogEntry? receivedEntry = null;
        ErrorLogger.EntryAdded += entry => receivedEntry = entry;

        ErrorLogger.Log("TestSource", "Test message");

        Assert.NotNull(receivedEntry);
        Assert.Equal("TestSource", receivedEntry.Source);
        Assert.Equal("Test message", receivedEntry.Message);

        // Cleanup event handler
        ErrorLogger.EntryAdded -= entry => receivedEntry = entry;
    }

    [Fact]
    public void MultipleEntries_MaintainOrder()
    {
        ErrorLogger.Log("Source1", "First");
        ErrorLogger.Log("Source2", "Second");
        ErrorLogger.Log("Source3", "Third");

        Assert.Equal(3, ErrorLogger.LogEntries.Count);
        Assert.Equal("First", ErrorLogger.LogEntries[0].Message);
        Assert.Equal("Second", ErrorLogger.LogEntries[1].Message);
        Assert.Equal("Third", ErrorLogger.LogEntries[2].Message);
    }

    [Fact]
    public void Timestamp_IsApproximatelyNow()
    {
        var before = DateTime.Now.AddSeconds(-1);
        ErrorLogger.Log("Source", "Message");
        var after = DateTime.Now.AddSeconds(1);

        Assert.InRange(ErrorLogger.LogEntries[0].Timestamp, before, after);
    }
}
