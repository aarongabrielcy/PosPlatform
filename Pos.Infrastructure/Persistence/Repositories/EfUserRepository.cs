using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.Users;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfUserRepository : IUserRepository
{
    private readonly PosDbContext _context;

    public EfUserRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<User?> GetByIdAsync(UserId userId, CancellationToken cancellationToken)
    {
        var record = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == userId.Value, cancellationToken);

        return record is null ? null : UserMapper.ToDomain(record);
    }

    public async Task<User?> GetByUsernameAsync(
        OrganizationId organizationId, string username, CancellationToken cancellationToken)
    {
        var record = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(
                r => r.OrganizationId == organizationId.Value && r.Username == username,
                cancellationToken);

        return record is null ? null : UserMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<User>> GetByOrganizationAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var records = await _context.Users
            .AsNoTracking()
            .Where(r => r.OrganizationId == organizationId.Value)
            .OrderBy(r => r.Username)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(UserMapper.ToDomain).ToList();
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var record = UserMapper.ToRecord(user);

        await _context.Users.AddAsync(record, cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        var record = await _context.Users
            .SingleOrDefaultAsync(r => r.Id == user.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("User", user.Id.ToString());
        }

        UserMapper.UpdateRecord(user, record);
    }
}
