using Pos.Domain.Common.Identifiers;

namespace Pos.Application.SalesCart;

public sealed record UpdateCartLineQuantityRequest(ProductId ProductId, decimal NewQuantity);
