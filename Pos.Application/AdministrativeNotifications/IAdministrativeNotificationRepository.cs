using Pos.Domain.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.AdministrativeNotifications;

// Command (TAREA 24E, sección 19): AddAsync es append-only (igual patrón que
// IProductAuditRepository), MarkReadAsync es la única mutación permitida (el receipt de un User
// puntual). Nunca expone Delete ni Update del contenido de la Notification.
public interface IAdministrativeNotificationRepository
{
    Task AddAsync(AdministrativeNotification notification, CancellationToken cancellationToken);

    // Idempotente: si el receipt ya estaba leído, no cambia ReadAtUtc (TAREA 24E, sección 22).
    Task MarkReadAsync(
        AdministrativeNotificationId notificationId,
        UserId userId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken);
}
