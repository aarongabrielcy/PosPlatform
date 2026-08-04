using Pos.Domain.Common.Identifiers;
using Pos.Domain.ProductAudit;

namespace Pos.Application.ProductAudit;

// Ninguno de los campos es obligatorio (TAREA 24D, sección 20): el módulo administrativo global
// no exige que la UI aplique todos los filtros a la vez. ActorSearchTerm es la alternativa cuando
// la UI no tiene un selector de usuarios (búsqueda textual sobre username/displayName).
public sealed record ProductAuditFilter(
    ProductId? ProductId = null,
    string? SearchTerm = null,
    UserId? ActorUserId = null,
    string? ActorSearchTerm = null,
    ProductAuditAction? Action = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    // Navegación exacta desde una notificación administrativa (TAREA 24E, sección 30/47): permite
    // seleccionar un ProductAuditEvent puntual sin depender de que esté en la primera página.
    ProductAuditEventId? AuditEventId = null)
{
    public static ProductAuditFilter Empty { get; } = new();
}
