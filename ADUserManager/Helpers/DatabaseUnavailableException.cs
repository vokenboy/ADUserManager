namespace ActiveManager.Helpers;

internal class DatabaseUnavailableException : Exception
{
    public DatabaseUnavailableException(string message) : base(message) { }
}
