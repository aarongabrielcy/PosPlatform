using Pos.Domain.Sales;

namespace Pos.Application.Receipts;

// Reference solo tiene contenido para Card (Manual Card): la referencia/autorización de la
// terminal externa que el cajero copia manualmente. Nunca contiene PAN/CVV/datos de tarjeta (la
// invariante vive en Pos.Domain.Sales.Payment, ver TAREA 25C-FIX).
public sealed record ReceiptPayment(PaymentMethod Method, decimal Amount, string? Reference);
