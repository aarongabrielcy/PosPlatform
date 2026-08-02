namespace Pos.Application.Sales.Checkout;

// Alcance 25A: único método de pago funcional es Efectivo (Card queda visible-deshabilitada en
// Desktop hasta TAREA 25A.1). CashTendered es el efectivo entregado por el cliente, no el importe
// que se registra en Payment.Amount (ver CheckoutService).
public sealed record CheckoutRequest(decimal CashTendered);
