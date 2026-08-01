using Pos.Application.RegisterSessions;

namespace Pos.Application.Tests.Products.CreateProduct;

internal sealed class FakeCurrentRegisterSession : ICurrentRegisterSession
{
    public ActiveRegisterSession? Current { get; set; }

    public bool IsOpen => Current is not null;

    public void SetActiveSession(ActiveRegisterSession activeSession)
    {
        Current = activeSession;
    }

    public void Clear()
    {
        Current = null;
    }
}
