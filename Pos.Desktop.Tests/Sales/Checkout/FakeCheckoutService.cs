using Pos.Application.Sales.Checkout;

namespace Pos.Desktop.Tests.Sales.Checkout;

internal sealed class FakeCheckoutService : ICheckoutService
{
    private readonly Func<CheckoutRequest, CancellationToken, Task<CheckoutResult>>? _checkoutHandler;

    public FakeCheckoutService(Func<CheckoutRequest, CancellationToken, Task<CheckoutResult>>? checkoutHandler = null)
    {
        _checkoutHandler = checkoutHandler;
    }

    public int CheckoutCallCount { get; private set; }

    public CheckoutRequest? LastRequest { get; private set; }

    public Task<CheckoutResult> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        CheckoutCallCount++;
        LastRequest = request;

        return _checkoutHandler is null
            ? Task.FromResult(CheckoutResult.Failure(CheckoutResultStatus.RegisterSessionRequired))
            : _checkoutHandler(request, cancellationToken);
    }
}
