namespace Pos.Application.Receipts;

// Proyección de una línea de ticket, derivada exclusivamente del snapshot histórico persistido en
// SaleHistoryDetailLine: nunca se reconsulta Product actual (ver Pos.Application.Sales.History).
public sealed record ReceiptLine(
    string ProductSku,
    string ProductName,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
