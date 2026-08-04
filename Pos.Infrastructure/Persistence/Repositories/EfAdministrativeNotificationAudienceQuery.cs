using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Infrastructure.Persistence.Repositories;

// Un solo JOIN users/roles/role_permissions, sin N+1 (TAREA 24E, sección 9): Organization actual,
// User activo, Role activo, permission solicitado.
public sealed class EfAdministrativeNotificationAudienceQuery : IAdministrativeNotificationAudienceQuery
{
    private readonly PosDbContext _context;

    public EfAdministrativeNotificationAudienceQuery(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<UserId>> GetEligibleUserIdsAsync(
        OrganizationId organizationId, Permission permission, CancellationToken cancellationToken)
    {
        var permissionName = permission.ToString();

        var userIds =
            from user in _context.Users.AsNoTracking()
            join role in _context.Roles.AsNoTracking() on user.RoleId equals role.Id
            join rolePermission in _context.RolePermissions.AsNoTracking() on role.Id equals rolePermission.RoleId
            where user.OrganizationId == organizationId.Value
                && user.IsActive
                && role.IsActive
                && rolePermission.Permission == permissionName
            select user.Id;

        var distinctIds = await userIds.Distinct().ToListAsync(cancellationToken);

        return distinctIds.Select(id => new UserId(id)).ToList();
    }
}
