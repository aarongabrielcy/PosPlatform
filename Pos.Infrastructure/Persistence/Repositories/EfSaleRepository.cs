using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Application.Sales;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

public sealed class EfSaleRepository : ISaleRepository
{
    private readonly PosDbContext _context;

    public EfSaleRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Sale?> GetByIdAsync(SaleId saleId, CancellationToken cancellationToken)
    {
        var record = await _context.Sales
            .AsNoTracking()
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.Id == saleId.Value, cancellationToken);

        return record is null ? null : SaleMapper.ToDomain(record);
    }

    public async Task AddAsync(Sale sale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sale);

        var record = SaleMapper.ToRecord(sale);

        await _context.Sales.AddAsync(record, cancellationToken);
    }

    public async Task<decimal> GetCompletedCashTotalByRegisterSessionAsync(
        RegisterSessionId registerSessionId, CancellationToken cancellationToken)
    {
        // SQLite no soporta SUM sobre columnas decimal en el servidor (EF Core lo rechaza en
        // tiempo de traducción): se trae la lista de importes y se suma en memoria. El volumen de
        // pagos en efectivo de una sola RegisterSession es acotado, así que no representa un
        // problema de rendimiento.
        var amounts = await _context.Sales
            .Where(r => r.RegisterSessionId == registerSessionId.Value && r.Status == SaleStatus.Completed)
            .SelectMany(r => r.Payments.Where(p => p.Method == PaymentMethod.Cash))
            .Select(p => p.Amount)
            .ToListAsync(cancellationToken);

        return amounts.Sum();
    }

    public async Task UpdateAsync(Sale sale, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sale);

        var record = await _context.Sales
            .Include(r => r.Lines)
            .Include(r => r.Payments)
            .SingleOrDefaultAsync(r => r.Id == sale.Id.Value, cancellationToken);

        if (record is null)
        {
            throw new EntityNotFoundException("Sale", sale.Id.ToString());
        }

        var trackedLineIds = record.Lines.Select(line => line.Id).ToHashSet();
        var trackedPaymentIds = record.Payments.Select(payment => payment.Id).ToHashSet();

        SaleMapper.UpdateRecord(sale, record);

        // EF Core trata las entidades nuevas con clave Guid ya asignada (no vacía) alcanzadas
        // solo por fixup de navegación como Modified en lugar de Added; se marcan explícitamente.
        foreach (var line in record.Lines)
        {
            if (!trackedLineIds.Contains(line.Id))
            {
                _context.Entry(line).State = EntityState.Added;
            }
        }

        foreach (var payment in record.Payments)
        {
            if (!trackedPaymentIds.Contains(payment.Id))
            {
                _context.Entry(payment).State = EntityState.Added;
            }
        }
    }
}
