namespace Pos.Application.Authentication;

public enum AuthenticationStatus
{
    Success,
    InvalidCredentials,
    InactiveUser,
    InactiveRole,
    InvalidInstallationState,
}
