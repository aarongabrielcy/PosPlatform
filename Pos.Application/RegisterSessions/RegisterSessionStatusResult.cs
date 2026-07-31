namespace Pos.Application.RegisterSessions;

public sealed class RegisterSessionStatusResult
{
    public RegisterSessionStatus Status { get; }

    public ActiveRegisterSession? ActiveSession { get; }

    private RegisterSessionStatusResult(RegisterSessionStatus status, ActiveRegisterSession? activeSession)
    {
        Status = status;
        ActiveSession = activeSession;
    }

    public static RegisterSessionStatusResult NoneOpen() => new(RegisterSessionStatus.NoneOpen, null);

    public static RegisterSessionStatusResult Open(ActiveRegisterSession activeSession)
    {
        ArgumentNullException.ThrowIfNull(activeSession);

        return new RegisterSessionStatusResult(RegisterSessionStatus.Open, activeSession);
    }

    public static RegisterSessionStatusResult InvalidState() => new(RegisterSessionStatus.InvalidState, null);
}
