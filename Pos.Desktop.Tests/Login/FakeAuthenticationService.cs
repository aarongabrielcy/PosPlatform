using Pos.Application.Authentication;

namespace Pos.Desktop.Tests.Login;

internal sealed class FakeAuthenticationService : IAuthenticationService
{
    private readonly Func<AuthenticationRequest, CancellationToken, Task<AuthenticationResult>> _handler;

    public FakeAuthenticationService(
        Func<AuthenticationRequest, CancellationToken, Task<AuthenticationResult>> handler) =>
        _handler = handler;

    public int CallCount { get; private set; }

    public AuthenticationRequest? LastRequest { get; private set; }

    public Task<AuthenticationResult> AuthenticateAsync(
        AuthenticationRequest request, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastRequest = request;

        return _handler(request, cancellationToken);
    }
}
