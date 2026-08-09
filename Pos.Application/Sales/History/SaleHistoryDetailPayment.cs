using Pos.Domain.Sales;

namespace Pos.Application.Sales.History;

// Proyección directa de Payment persistido (TAREA 25B, sección 6/25): no incluye CashTendered ni
// Change porque Payment no los persiste (son transitorios del checkout, ver CheckoutService).
public sealed record SaleHistoryDetailPayment(PaymentMethod Method, decimal Amount, string Currency, DateTimeOffset PaidAtUtc);
