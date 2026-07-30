using Pos.Application.Authentication;

namespace Pos.Application.Tests.Authentication;

internal sealed class FakeCurrentUserSessionWriter : ICurrentUserSessionWriter
{
    public int SetAuthenticatedUserCallCount { get; private set; }

    public AuthenticatedUser? LastAuthenticatedUser { get; private set; }

    public void SetAuthenticatedUser(AuthenticatedUser authenticatedUser)
    {
        SetAuthenticatedUserCallCount++;
        LastAuthenticatedUser = authenticatedUser;
    }
}
