using Pos.Domain.Sales;

namespace Pos.Application.Sales.History;

// Una venta puede tener varios Payments (Sale.Payments es una colección, TAREA 25B, sección 18);
// este DTO representa el monto agregado por método, usado tanto en el desglose de una venta
// individual (SalesHistoryItem.PaymentSummary) como en el resumen global del filtro
// (SalesHistorySummary.PaymentBreakdown).
public sealed record SalesHistoryPaymentAmount(PaymentMethod Method, decimal Amount);
