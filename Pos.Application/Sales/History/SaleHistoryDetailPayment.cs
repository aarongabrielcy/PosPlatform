using Pos.Domain.Sales;

namespace Pos.Application.Sales.History;

// Proyección directa de Payment persistido (TAREA 25B, sección 6/25; TAREA 25C): no incluye
// CashTendered ni Change porque Payment no los persiste (son transitorios del checkout, ver
// CheckoutService). Reference solo tiene valor para métodos distintos de Cash (Manual Card):
// referencia/autorización de la terminal externa, nunca datos sensibles de tarjeta.
public sealed record SaleHistoryDetailPayment(
    PaymentMethod Method, decimal Amount, string Currency, DateTimeOffset PaidAtUtc, string? Reference);
