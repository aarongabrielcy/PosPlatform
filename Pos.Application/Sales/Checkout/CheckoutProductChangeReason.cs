namespace Pos.Application.Sales.Checkout;

// Motivo por el cual CheckoutService rechazó el cobro con status ProductChanged (TAREA 25A
// sección 8): el producto cambió un dato relevante desde que se agregó al carrito y no se
// completa la venta silenciosamente con datos desactualizados.
public enum CheckoutProductChangeReason
{
    Sku,
    Name,
    Price,
}
