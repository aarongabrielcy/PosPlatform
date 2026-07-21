using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;

namespace Pos.Application.Sales.CompleteSale;

public sealed record CompleteSaleResult(
    SaleId SaleId,
    SaleStatus Status,
    DateTimeOffset CompletedAtUtc,
    int InventoryMovementsCreated);
