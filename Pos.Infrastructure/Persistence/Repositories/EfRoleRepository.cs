using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.Security;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Persistence.Mappers;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfRoleRepository : IRoleRepository
{
    private readonly PosDbContext _context;

    public EfRoleRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Role?> GetByIdAsync(RoleId roleId, CancellationToken cancellationToken)
    {
        var record = await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == roleId.Value, cancellationToken);

        return record is null ? null : RoleMapper.ToDomain(record);
    }

    public async Task<Role?> GetByNameAsync(OrganizationId organizationId, string name, CancellationToken cancellationToken)
    {
        var record = await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(
                r => r.OrganizationId == organizationId.Value && r.Name == name,
                cancellationToken);

        return record is null ? null : RoleMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Role>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var records = await _context.Roles
            .AsNoTracking()
            .Include(r => r.Permissions)
            .Where(r => r.OrganizationId == organizationId.Value)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(RoleMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        var record = RoleMapper.ToRecord(role);

        await _context.Roles.AddAsync(record, cancellationToken);
    }

    public async Task UpdateAsync(Role role, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(role);

        var record = await _context.Roles
            .Include(r => r.Permissions)
            .SingleOrDefaultAsync(r => r.Id == role.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("Role", role.Id.ToString());
        }

        RoleMapper.UpdateRecord(role, record);
    }
}
