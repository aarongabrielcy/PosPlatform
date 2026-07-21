namespace Pos.Infrastructure.Persistence.Initialization;

public sealed class LocalDatabaseInitializationException : Exception
{
    public LocalDatabaseInitializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
