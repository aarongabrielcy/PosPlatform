using Pos.Domain.Common.Identifiers;

namespace Pos.Application.AdministrativeNotifications;

// Punto de entrada para Desktop (TAREA 24E, sección 21): exige usuario autenticado con
// ViewProductAudit, deriva OrganizationId/UserId de la sesión actual y nunca acepta un UserId
// arbitrario desde la UI. MarkReadAsync solo puede modificar el receipt del usuario actual.
public interface IAdministrativeNotificationService
{
    Task<AdministrativeNotificationPageResult> GetNotificationsAsync(
        int skip, int take, CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(CancellationToken cancellationToken = default);

    // Idempotente; retorna false si el usuario no está autenticado o no tiene ViewProductAudit.
    Task<bool> MarkReadAsync(AdministrativeNotificationId notificationId, CancellationToken cancellationToken = default);
}
