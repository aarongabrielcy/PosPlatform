using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;

namespace Pos.Application.Organizations;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    Task<Organization?> GetFirstAsync(CancellationToken cancellationToken);

    Task AddAsync(Organization organization, CancellationToken cancellationToken);
}
