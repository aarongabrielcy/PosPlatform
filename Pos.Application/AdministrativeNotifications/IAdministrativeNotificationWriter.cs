using Pos.Domain.ProductAudit;

namespace Pos.Application.AdministrativeNotifications;

// Evalúa la política y prepara (sin CommitAsync) la AdministrativeNotification correspondiente a
// un ProductAuditEvent recién construido, para que el servicio Product que la invoca la persista
// en el mismo commit que Product/Audit/InventoryMovement (TAREA 24E, sección 10/11).
public interface IAdministrativeNotificationWriter
{
    Task TryAddForProductAuditAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken);
}
