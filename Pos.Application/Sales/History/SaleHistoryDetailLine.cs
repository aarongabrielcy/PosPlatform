namespace Pos.Application.Sales.History;

// Proyección directa de SaleLine persistido (TAREA 25B, sección 6/39): ProductSku/ProductName/
// UnitPrice son los snapshots reales guardados al momento de la venta, nunca se consulta Product
// actual para reconstruir estos valores.
public sealed record SaleHistoryDetailLine(
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency);
