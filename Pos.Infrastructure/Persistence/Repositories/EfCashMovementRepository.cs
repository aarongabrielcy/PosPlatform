using Microsoft.EntityFrameworkCore;
using Pos.Application.CashMovements;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfCashMovementRepository : ICashMovementRepository
{
    private readonly PosDbContext _context;

    public EfCashMovementRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(CashMovement movement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(movement);

        var record = CashMovementMapper.ToRecord(movement);

        await _context.CashMovements.AddAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<CashMovement>> GetByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        var records = await _context.CashMovements
            .AsNoTracking()
            .Where(r => r.RegisterSessionId == registerSessionId.Value)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return records.Select(CashMovementMapper.ToDomain).ToList();
    }

    public async Task<decimal> GetCashInTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
        await SumByTypeAsync(registerSessionId, CashMovementType.CashIn, cancellationToken);

    public async Task<decimal> GetCashOutTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken) =>
        await SumByTypeAsync(registerSessionId, CashMovementType.CashOut, cancellationToken);

    // SQLite no soporta SUM sobre columnas decimal en el servidor (mismo límite documentado en
    // EfSaleRepository): se trae la lista de importes y se suma en memoria. El volumen de
    // movimientos de una sola RegisterSession es acotado.
    private async Task<decimal> SumByTypeAsync(
        RegisterSessionId registerSessionId, CashMovementType type, CancellationToken cancellationToken)
    {
        var amounts = await _context.CashMovements
            .Where(r => r.RegisterSessionId == registerSessionId.Value && r.Type == type)
            .Select(r => r.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }
}
