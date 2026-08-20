using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Security;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken);

    Task<Role?> GetByNameAsync(OrganizationId organizationId, string name, CancellationToken cancellationToken);

    Task<IReadOnlyList<Role>> GetByOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    Task AddAsync(Role role, CancellationToken cancellationToken);

    Task UpdateAsync(Role role, CancellationToken cancellationToken);
}
