namespace Pos.Application.RegisterSessions;

public enum RegisterSessionResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InvalidInstallationState,
    RegisterNotFound,
    RegisterInactive,
    RegisterSelectionRequired,
    AlreadyOpen,
    InvalidAmount,
    SessionNotFound,
    SessionAlreadyClosed,
    SessionBelongsToAnotherOrganization,
}
