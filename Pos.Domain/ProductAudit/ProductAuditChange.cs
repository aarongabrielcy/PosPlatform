using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.ProductAudit;

// Fila hija append-only de ProductAuditEvent: un campo, su valor anterior y su valor nuevo
// (TAREA 24D, sección 6). Nunca se crea con OldValue == NewValue (ver ProductAuditEvent, que es
// quien construye instancias a partir de los cambios detectados por Application).
public sealed class ProductAuditChange
{
    public ProductAuditChangeId Id { get; }

    public ProductAuditEventId ProductAuditEventId { get; }

    public ProductAuditField FieldName { get; }

    public string? OldValue { get; }

    public string? NewValue { get; }

    internal ProductAuditChange(
        ProductAuditChangeId id,
        ProductAuditEventId productAuditEventId,
        ProductAuditField fieldName,
        string? oldValue,
        string? newValue)
    {
        Id = EnsureNotEmpty(id);
        ProductAuditEventId = EnsureNotEmpty(productAuditEventId);
        FieldName = EnsureDefined(fieldName);

        if (oldValue == newValue)
        {
            throw new DomainValidationException(
                $"ProductAuditChange para '{fieldName}' no puede tener OldValue igual a NewValue.");
        }

        OldValue = oldValue;
        NewValue = newValue;
    }

    private static ProductAuditChangeId EnsureNotEmpty(ProductAuditChangeId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static ProductAuditEventId EnsureNotEmpty(ProductAuditEventId productAuditEventId)
    {
        if (productAuditEventId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductAuditEventId no puede ser vacío.");
        }

        return productAuditEventId;
    }

    private static ProductAuditField EnsureDefined(ProductAuditField fieldName)
    {
        if (!Enum.IsDefined(fieldName))
        {
            throw new DomainValidationException("FieldName no es un valor válido de ProductAuditField.");
        }

        return fieldName;
    }
}
