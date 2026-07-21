namespace Pos.Infrastructure.Persistence.Exceptions;

public sealed class PersistenceDataException : Exception
{
    public PersistenceDataException(string message)
        : base(message)
    {
    }

    public PersistenceDataException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
