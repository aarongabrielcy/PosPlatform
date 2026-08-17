namespace Pos.Application.Sales.Checkout;

// TAREA 25C: CashTendered conserva su posición original (compatibilidad con Alcance 25A) y solo
// aplica cuando PaymentMethod es Cash; se ignora para Card. CardReference solo aplica cuando
// PaymentMethod es Card: la referencia/autorización que el cajero copia de la terminal externa tras
// una autorización aprobada. CashTendered es el efectivo entregado por el cliente, no el importe que
// se registra en Payment.Amount (ver CheckoutService).
public sealed record CheckoutRequest(
    decimal CashTendered,
    CheckoutPaymentMethod PaymentMethod = CheckoutPaymentMethod.Cash,
    string? CardReference = null);
