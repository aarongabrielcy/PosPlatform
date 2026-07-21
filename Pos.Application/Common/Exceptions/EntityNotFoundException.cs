namespace Pos.Application.Common.Exceptions;

public sealed class EntityNotFoundException : Exception
{
    public EntityNotFoundException(string entityName, string identifier)
        : base($"{ValidateEntityName(entityName)} with identifier '{ValidateIdentifier(identifier)}' was not found.")
    {
    }

    private static string ValidateEntityName(string entityName)
    {
        if (string.IsNullOrWhiteSpace(entityName))
        {
            throw new ArgumentException("entityName no puede estar vacío.", nameof(entityName));
        }

        return entityName;
    }

    private static string ValidateIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("identifier no puede estar vacío.", nameof(identifier));
        }

        return identifier;
    }
}
