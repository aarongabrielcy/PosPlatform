using Pos.Application.RegisterSessions;

namespace Pos.Application.Tests.RegisterSessions;

internal sealed class FakeCurrentRegisterSession : ICurrentRegisterSession
{
    public ActiveRegisterSession? Current { get; private set; }

    public bool IsOpen => Current is not null;

    public int SetCallCount { get; private set; }

    public int ClearCallCount { get; private set; }

    public void SetActiveSession(ActiveRegisterSession activeSession)
    {
        SetCallCount++;
        Current = activeSession;
    }

    public void Clear()
    {
        ClearCallCount++;
        Current = null;
    }
}
