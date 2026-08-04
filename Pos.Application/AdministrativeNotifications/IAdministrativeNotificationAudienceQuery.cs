using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.AdministrativeNotifications;

// Resuelve la audiencia administrativa de una Organization para un Permission dado, en una sola
// consulta (JOIN users/roles/role_permissions), sin N+1 (TAREA 24E, sección 9). V1 siempre se
// invoca con Permission.ViewProductAudit; el actor no se excluye (sección 8).
public interface IAdministrativeNotificationAudienceQuery
{
    Task<IReadOnlyList<UserId>> GetEligibleUserIdsAsync(
        OrganizationId organizationId, Permission permission, CancellationToken cancellationToken);
}
