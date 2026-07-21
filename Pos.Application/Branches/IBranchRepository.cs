using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Branches;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(BranchId branchId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Branch>> GetByOrganizationAsync(OrganizationId organizationId, CancellationToken cancellationToken);

    Task AddAsync(Branch branch, CancellationToken cancellationToken);
}
