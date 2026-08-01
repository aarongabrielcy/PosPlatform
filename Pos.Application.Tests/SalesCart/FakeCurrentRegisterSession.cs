using Pos.Application.RegisterSessions;

namespace Pos.Application.Tests.SalesCart;

internal sealed class FakeCurrentRegisterSession : ICurrentRegisterSession
{
    public ActiveRegisterSession? Current { get; set; }

    public bool IsOpen => Current is not null;

    public int ClearCallCount { get; private set; }

    public void SetActiveSession(ActiveRegisterSession activeSession)
    {
        Current = activeSession;
    }

    public void Clear()
    {
        ClearCallCount++;
        Current = null;
    }
}
