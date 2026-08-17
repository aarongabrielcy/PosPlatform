namespace Pos.Application.Sales.Checkout;

public enum CheckoutResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    RegisterSessionRequired,
    EmptyCart,
    CurrencyMismatch,
    ProductNotFound,
    ProductInactive,
    ProductChanged,
    InsufficientStock,
    InvalidPayment,
    InsufficientCash,
    InvalidCardReference,
    InternalValidationError,
}
