using Microsoft.EntityFrameworkCore;
using Pos.Application.Registers;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfRegisterRepository : IRegisterRepository
{
    private readonly PosDbContext _context;

    public EfRegisterRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Register?> GetByIdAsync(RegisterId registerId, CancellationToken cancellationToken)
    {
        var record = await _context.Registers
            .AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == registerId.Value, cancellationToken);

        return record is null ? null : RegisterMapper.ToDomain(record);
    }

    public async Task<IReadOnlyList<Register>> GetByBranchAsync(BranchId branchId, CancellationToken cancellationToken)
    {
        var records = await _context.Registers
            .AsNoTracking()
            .Where(r => r.BranchId == branchId.Value)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

        return records.Select(RegisterMapper.ToDomain).ToList();
    }

    public async Task AddAsync(Register cashRegister, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cashRegister);

        var record = RegisterMapper.ToRecord(cashRegister);

        await _context.Registers.AddAsync(record, cancellationToken);
    }
}
