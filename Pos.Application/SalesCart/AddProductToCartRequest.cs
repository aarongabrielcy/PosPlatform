using Pos.Domain.Common.Identifiers;

namespace Pos.Application.SalesCart;

public sealed record AddProductToCartRequest(ProductId ProductId, decimal Quantity = 1m);
