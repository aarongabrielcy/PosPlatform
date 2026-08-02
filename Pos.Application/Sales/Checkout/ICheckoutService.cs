namespace Pos.Application.Sales.Checkout;

public interface ICheckoutService
{
    Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
}
