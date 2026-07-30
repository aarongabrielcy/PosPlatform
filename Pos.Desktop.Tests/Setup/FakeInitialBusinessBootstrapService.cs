using Pos.Application.Bootstrap;

namespace Pos.Desktop.Tests.Setup;

internal sealed class FakeInitialBusinessBootstrapService : IInitialBusinessBootstrapService
{
    private readonly Func<InitialBusinessBootstrapRequest, CancellationToken, Task<InitialBusinessBootstrapResult>> _handler;

    public FakeInitialBusinessBootstrapService(
        Func<InitialBusinessBootstrapRequest, CancellationToken, Task<InitialBusinessBootstrapResult>> handler) =>
        _handler = handler;

    public int CallCount { get; private set; }

    public InitialBusinessBootstrapRequest? LastRequest { get; private set; }

    public Task<InitialBusinessBootstrapResult> BootstrapAsync(
        InitialBusinessBootstrapRequest request, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        return _handler(request, cancellationToken);
    }
}
