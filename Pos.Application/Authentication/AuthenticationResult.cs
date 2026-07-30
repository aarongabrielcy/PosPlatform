namespace Pos.Application.Authentication;

public sealed class AuthenticationResult
{
    public AuthenticationStatus Status { get; }

    public AuthenticatedUser? AuthenticatedUser { get; }

    private AuthenticationResult(AuthenticationStatus status, AuthenticatedUser? authenticatedUser)
    {
        Status = status;
        AuthenticatedUser = authenticatedUser;
    }

    public static AuthenticationResult Success(AuthenticatedUser authenticatedUser)
    {
        ArgumentNullException.ThrowIfNull(authenticatedUser);

        return new AuthenticationResult(AuthenticationStatus.Success, authenticatedUser);
    }

    public static AuthenticationResult Failure(AuthenticationStatus status)
    {
        if (status == AuthenticationStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere un AuthenticatedUser; use AuthenticationResult.Success(...).", nameof(status));
        }

        return new AuthenticationResult(status, null);
    }

    // No expone password ni hash: solo el estado y, en éxito, la identidad autenticada.
    public override string ToString() => $"AuthenticationResult[Status={Status}]";
}
