namespace Pos.Application.RegisterSessions;

public sealed class RegisterSessionResult
{
    public RegisterSessionResultStatus Status { get; }

    public bool Success => Status == RegisterSessionResultStatus.Success;

    public ActiveRegisterSession? ActiveSession { get; }

    public RegisterSessionSummary? Summary { get; }

    private RegisterSessionResult(
        RegisterSessionResultStatus status, ActiveRegisterSession? activeSession, RegisterSessionSummary? summary)
    {
        Status = status;
        ActiveSession = activeSession;
        Summary = summary;
    }

    public static RegisterSessionResult OpenSuccess(ActiveRegisterSession activeSession)
    {
        ArgumentNullException.ThrowIfNull(activeSession);

        return new RegisterSessionResult(RegisterSessionResultStatus.Success, activeSession, null);
    }

    public static RegisterSessionResult CloseSuccess(RegisterSessionSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RegisterSessionResult(RegisterSessionResultStatus.Success, null, summary);
    }

    public static RegisterSessionResult Failure(RegisterSessionResultStatus status)
    {
        if (status == RegisterSessionResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere ActiveSession o Summary; use OpenSuccess/CloseSuccess.", nameof(status));
        }

        return new RegisterSessionResult(status, null, null);
    }
}
