namespace Pos.Application.SalesCart;

public enum SalesCartResultStatus
{
    Success,
    NotAuthenticated,
    RegisterSessionRequired,
    ProductNotFound,
    ProductInactive,
    OutOfStock,
    InsufficientStock,
    InvalidQuantity,
    LineNotFound,
    CurrencyMismatch,
}
