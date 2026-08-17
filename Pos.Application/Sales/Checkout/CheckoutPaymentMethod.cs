namespace Pos.Application.Sales.Checkout;

// Método de pago solicitado por Desktop para un checkout (TAREA 25C). Tipo propio de Application
// (no Pos.Domain.Sales.PaymentMethod): Pos.Desktop no depende de Pos.Domain y nunca debe referenciar
// tipos de dominio directamente (mismo patrón que CheckoutResultStatus/CheckoutProductChangeReason).
// Card significa siempre "tarjeta cobrada en una terminal externa, ajena a PosPlatform" (Manual
// Card): no existe integración de terminal en Basic V1.
public enum CheckoutPaymentMethod
{
    Cash,
    Card
}
