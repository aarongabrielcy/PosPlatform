using Pos.Domain.Common.Identifiers;

namespace Pos.Application.AdministrativeNotifications;

// Consulta de solo lectura, siempre scoped por (OrganizationId, UserId): nunca se consulta la
// bandeja de otro usuario (TAREA 24E, sección 20/21).
public interface IAdministrativeNotificationQuery
{
    Task<AdministrativeNotificationPageResult> GetForUserAsync(
        OrganizationId organizationId, UserId userId, int skip, int take, CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(OrganizationId organizationId, UserId userId, CancellationToken cancellationToken);
}
