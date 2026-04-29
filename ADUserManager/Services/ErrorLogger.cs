namespace ActiveManager.Services;

public record ErrorLogEntry(DateTime Timestamp, string Source, string Message, string? StackTrace);

public static class ErrorLogger
{
    private static readonly List<ErrorLogEntry> _entries = new();

    public static IReadOnlyList<ErrorLogEntry> LogEntries => _entries;

    public static event Action<ErrorLogEntry>? EntryAdded;

    public static void Log(string source, string message, string? stackTrace = null)
    {
        var entry = new ErrorLogEntry(DateTime.Now, source, message, stackTrace);
        _entries.Add(entry);
        EntryAdded?.Invoke(entry);
    }

    public static void Log(string source, Exception ex)
    {
        Log(source, ex.Message, ex.ToString());
    }

    public static void Clear()
    {
        _entries.Clear();
    }
}
