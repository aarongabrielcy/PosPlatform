using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfRegisterSessionRepository : IRegisterSessionRepository
{
    private readonly PosDbContext _context;

    public EfRegisterSessionRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<RegisterSession?> GetByIdAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        var record = await _context.RegisterSessions
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == registerSessionId.Value, cancellationToken);

        return record is null ? null : RegisterSessionMapper.ToDomain(record);
    }

    public async Task<RegisterSession?> GetOpenByRegisterAsync(RegisterId registerId, CancellationToken cancellationToken)
    {
        var records = await _context.RegisterSessions
            .AsNoTracking()
            .Where(r => r.RegisterId == registerId.Value && r.Status == Pos.Domain.RegisterSessions.RegisterSessionStatus.Open)
            .Take(2)
            .ToListAsync(cancellationToken);

        if (records.Count == 0)
        {
            return null;
        }

        if (records.Count > 1)
        {
            throw new InvalidOperationException(
                $"El Register '{registerId}' tiene más de una RegisterSession abierta.");
        }

        return RegisterSessionMapper.ToDomain(records[0]);
    }

    public async Task AddAsync(RegisterSession registerSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registerSession);

        var record = RegisterSessionMapper.ToRecord(registerSession);

        await _context.RegisterSessions.AddAsync(record, cancellationToken);
    }

    public async Task UpdateAsync(RegisterSession registerSession, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registerSession);

        var record = await _context.RegisterSessions
            .SingleOrDefaultAsync(r => r.Id == registerSession.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("RegisterSession", registerSession.Id.ToString());
        }

        RegisterSessionMapper.UpdateRecord(registerSession, record);
    }
}
