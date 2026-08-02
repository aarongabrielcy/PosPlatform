namespace Pos.Application.RegisterSessions;

public sealed class RegisterClosingSummaryResult
{
    public RegisterSessionResultStatus Status { get; }

    public bool Success => Status == RegisterSessionResultStatus.Success;

    public RegisterClosingSummary? Summary { get; }

    private RegisterClosingSummaryResult(RegisterSessionResultStatus status, RegisterClosingSummary? summary)
    {
        Status = status;
        Summary = summary;
    }

    public static RegisterClosingSummaryResult SuccessResult(RegisterClosingSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new RegisterClosingSummaryResult(RegisterSessionResultStatus.Success, summary);
    }

    public static RegisterClosingSummaryResult Failure(RegisterSessionResultStatus status)
    {
        if (status == RegisterSessionResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere Summary; use SuccessResult.", nameof(status));
        }

        return new RegisterClosingSummaryResult(status, null);
    }
}
