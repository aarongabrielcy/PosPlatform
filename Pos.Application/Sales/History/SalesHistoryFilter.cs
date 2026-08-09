using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.History;

// Ninguno de los campos es obligatorio (TAREA 25B, sección 10/13): la UI puede combinar cualquier
// subconjunto de filtros. FromUtc/ToUtc ya vienen convertidos a UTC por el llamador (Desktop
// resuelve el día local antes de construir el filtro); esta capa nunca interpreta zona horaria.
// SearchTerm evalúa SaleId, Sku de línea y nombre snapshot de producto (sección 10).
public sealed record SalesHistoryFilter(
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    UserId? CashierUserId = null,
    RegisterId? RegisterId = null,
    PaymentMethod? PaymentMethod = null,
    string? SearchTerm = null)
{
    public static SalesHistoryFilter Empty { get; } = new();
}
