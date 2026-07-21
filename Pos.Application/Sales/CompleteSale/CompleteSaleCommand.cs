using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Sales.CompleteSale;

public sealed record CompleteSaleCommand(
    SaleId SaleId,
    UserId PerformedByUserId,
    DateTimeOffset CompletedAtUtc);
