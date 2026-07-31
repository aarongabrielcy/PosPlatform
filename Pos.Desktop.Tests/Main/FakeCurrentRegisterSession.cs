using Pos.Application.RegisterSessions;

namespace Pos.Desktop.Tests.Main;

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
