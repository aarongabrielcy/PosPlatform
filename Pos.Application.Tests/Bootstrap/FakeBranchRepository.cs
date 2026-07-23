using Pos.Application.Branches;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Tests.Bootstrap;

internal sealed class FakeBranchRepository : IBranchRepository
{
    private readonly List<Branch> _branches;

    public FakeBranchRepository(IEnumerable<Branch>? seed = null)
    {
        _branches = seed?.ToList() ?? [];
    }

    public int AddCallCount { get; private set; }

    public Task<Branch?> GetByIdAsync(BranchId branchId, CancellationToken cancellationToken) =>
        Task.FromResult(_branches.SingleOrDefault(b => b.Id == branchId));

    public Task<IReadOnlyList<Branch>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Branch>>(
            _branches.Where(b => b.OrganizationId == organizationId).ToList());

    public Task AddAsync(Branch branch, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _branches.Add(branch);

        return Task.CompletedTask;
    }
}
