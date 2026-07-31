namespace Pos.Application.RegisterSessions;

public interface ICurrentRegisterSession
{
    ActiveRegisterSession? Current { get; }

    bool IsOpen { get; }

    void SetActiveSession(ActiveRegisterSession activeSession);

    void Clear();
}
