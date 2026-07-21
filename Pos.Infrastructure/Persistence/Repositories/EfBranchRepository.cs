using Microsoft.EntityFrameworkCore;
using Pos.Application.Branches;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfBranchRepository : IBranchRepository
{
    private readonly PosDbContext _context;

    public EfBranchRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Branch?> GetByIdAsync(BranchId branchId, CancellationToken cancellationToken)
    {
        var record = await _context.Branches
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == branchId.Value, cancellationToken);

        return record is null ? null : BranchMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Branch>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var records = await _context.Branches
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId.Value)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(BranchMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Branch branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(branch);

        var record = BranchMapper.ToRecord(branch);

        await _context.Branches.AddAsync(record, cancellationToken);
    }
}
