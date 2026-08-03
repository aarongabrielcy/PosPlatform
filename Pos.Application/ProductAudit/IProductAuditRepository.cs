using Pos.Domain.ProductAudit;

namespace Pos.Application.ProductAudit;

// Repositorio command, append-only (TAREA 24D, sección 8/19): únicamente AddAsync, igual que
// IInventoryMovementRepository (Pos.Application.Inventory). Nunca expone Update/Delete.
public interface IProductAuditRepository
{
    Task AddAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken);
}
