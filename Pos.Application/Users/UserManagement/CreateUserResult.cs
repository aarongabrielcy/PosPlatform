namespace Pos.Application.Users.UserManagement;

public sealed class CreateUserResult
{
    public bool Success { get; }

    public CreateUserResultStatus Status { get; }

    public UserListItem? User { get; }

    private CreateUserResult(bool success, CreateUserResultStatus status, UserListItem? user)
    {
        Success = success;
        Status = status;
        User = user;
    }

    public static CreateUserResult SuccessResult(UserListItem user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new CreateUserResult(true, CreateUserResultStatus.Success, user);
    }

    public static CreateUserResult Failure(CreateUserResultStatus status)
    {
        if (status == CreateUserResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere un UserListItem; use CreateUserResult.SuccessResult(...).", nameof(status));
        }

        return new CreateUserResult(false, status, null);
    }
}
