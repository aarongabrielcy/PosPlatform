using Microsoft.EntityFrameworkCore;
using Pos.Application.Organizations;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfOrganizationRepository : IOrganizationRepository
{
    private readonly PosDbContext _context;

    public EfOrganizationRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Organization?> GetByIdAsync(OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var record = await _context.Organizations
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == organizationId.Value, cancellationToken);

        return record is null ? null : OrganizationMapper.ToDomain(record);
    }

    public async Task<Organization?> GetFirstAsync(CancellationToken cancellationToken)
    {
        var record = await _context.Organizations
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return record is null ? null : OrganizationMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Organization>> GetAllAsync(CancellationToken cancellationToken)
    {
        var records = await _context.Organizations
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(OrganizationMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(organization);

        var record = OrganizationMapper.ToRecord(organization);

        await _context.Organizations.AddAsync(record, cancellationToken);
    }
}
