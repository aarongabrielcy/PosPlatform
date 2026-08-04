using Pos.Application.AdministrativeNotifications;
using Pos.Domain.ProductAudit;

namespace Pos.Application.Tests.Products.ManageProduct;

internal sealed class FakeAdministrativeNotificationWriter : IAdministrativeNotificationWriter
{
    private readonly List<ProductAuditEvent> _auditEvents = [];

    public int CallCount { get; private set; }

    public IReadOnlyList<ProductAuditEvent> AuditEvents => _auditEvents;

    // Permite verificar, desde el test, que la llamada ocurre antes del CommitAsync del servicio
    // (TAREA 24E, sección 11/43): el test asigna un delegado que revisa CommitCallCount==0 aquí.
    public Action? OnCall { get; set; }

    public Task TryAddForProductAuditAsync(ProductAuditEvent auditEvent, CancellationToken cancellationToken)
    {
        CallCount++;
        _auditEvents.Add(auditEvent);
        OnCall?.Invoke();

        return Task.CompletedTask;
    }
}
