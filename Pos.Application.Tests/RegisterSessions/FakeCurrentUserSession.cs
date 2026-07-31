using Pos.Application.Authentication;

namespace Pos.Application.Tests.RegisterSessions;

internal sealed class FakeCurrentUserSession : ICurrentUserSession
{
    public AuthenticatedUser? CurrentUser { get; set; }

    public bool IsAuthenticated => CurrentUser is not null;

    public int ClearCallCount { get; private set; }

    public event EventHandler? SessionChanged;

    public void Clear()
    {
        ClearCallCount++;
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}
