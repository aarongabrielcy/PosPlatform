namespace Pos.Application.Users.UserManagement;

public enum CreateUserResultStatus
{
    Success,
    NotAuthenticated,
    NotAuthorized,
    InvalidUsername,
    InvalidDisplayName,
    InvalidPassword,
    InvalidRole,
    DuplicateUsername,
}
