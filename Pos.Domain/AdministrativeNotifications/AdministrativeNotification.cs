using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;

namespace Pos.Domain.AdministrativeNotifications;

// Notificación administrativa append-only (TAREA 24E): apunta a un ProductAuditEvent en vez de
// duplicar su contenido. Su existencia significa "este AuditEvent requiere atención
// administrativa"; la UI obtiene producto/actor/acción/changes desde IProductAuditQuery. Sin
// constructor público: solo Create, igual patrón que ProductAuditEvent.
public sealed class AdministrativeNotification
{
    private readonly List<AdministrativeNotificationRecipient> _recipients;

    public AdministrativeNotificationId Id { get; }

    public OrganizationId OrganizationId { get; }

    public ProductAuditEventId ProductAuditEventId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public IReadOnlyList<AdministrativeNotificationRecipient> Recipients => _recipients;

    private AdministrativeNotification(
        AdministrativeNotificationId id,
        OrganizationId organizationId,
        ProductAuditEventId productAuditEventId,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<UserId> recipientUserIds)
    {
        Id = EnsureNotEmpty(id);
        OrganizationId = EnsureNotEmpty(organizationId);
        ProductAuditEventId = EnsureNotEmpty(productAuditEventId);
        CreatedAtUtc = EnsureUtc(createdAtUtc);

        ArgumentNullException.ThrowIfNull(recipientUserIds);

        // Preferencia (TAREA 24E, sección 42): no debería existir una Notification sin
        // destinatarios; el writer ya evita llamar a Create en ese caso, pero el invariante
        // también se protege aquí.
        if (recipientUserIds.Count == 0)
        {
            throw new DomainValidationException("Una AdministrativeNotification debe tener al menos un destinatario.");
        }

        if (recipientUserIds.Distinct().Count() != recipientUserIds.Count)
        {
            throw new DomainValidationException("Una AdministrativeNotification no puede duplicar destinatarios.");
        }

        _recipients = recipientUserIds
            .Select(userId => new AdministrativeNotificationRecipient(Id, userId))
            .ToList();
    }

    public static AdministrativeNotification Create(
        AdministrativeNotificationId id,
        OrganizationId organizationId,
        ProductAuditEventId productAuditEventId,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<UserId> recipientUserIds) =>
        new(id, organizationId, productAuditEventId, createdAtUtc, recipientUserIds);

    private static AdministrativeNotificationId EnsureNotEmpty(AdministrativeNotificationId id)
    {
        if (id.Value == Guid.Empty)
        {
            throw new DomainValidationException("Id no puede ser vacío.");
        }

        return id;
    }

    private static OrganizationId EnsureNotEmpty(OrganizationId organizationId)
    {
        if (organizationId.Value == Guid.Empty)
        {
            throw new DomainValidationException("OrganizationId no puede ser vacío.");
        }

        return organizationId;
    }

    private static ProductAuditEventId EnsureNotEmpty(ProductAuditEventId productAuditEventId)
    {
        if (productAuditEventId.Value == Guid.Empty)
        {
            throw new DomainValidationException("ProductAuditEventId no puede ser vacío.");
        }

        return productAuditEventId;
    }

    private static DateTimeOffset EnsureUtc(DateTimeOffset value)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new DomainValidationException("CreatedAtUtc debe tener Offset igual a TimeSpan.Zero.");
        }

        return value;
    }
}
