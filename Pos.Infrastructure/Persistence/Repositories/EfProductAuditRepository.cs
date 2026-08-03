using Pos.Application.ProductAudit;
using Pos.Domain.ProductAudit;
using Pos.Infrastructure.Persistence.Mappers;

namespace Pos.Infrastructure.Persistence.Repositories;

// Command, append-only (TAREA 24D, sección 19): únicamente AddAsync, igual que
// EfInventoryMovementRepository. Nunca expone Update/Delete.
public sealed class EfProductAuditRepository : IProductAuditRepository
{
    private readonly PosDbContext _context;

    public EfProductAuditRepository(PosDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var record = ProductAuditEventMapper.ToRecord(auditEvent);

        await _context.ProductAuditEvents.AddAsync(record, cancellationToken);
    }
}
