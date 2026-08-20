namespace Pos.Application.Users.UserManagement;

public sealed class UserOperationResult
{
    public bool Success { get; }

    public UserOperationResultStatus Status { get; }

    public UserListItem? User { get; }

    private UserOperationResult(bool success, UserOperationResultStatus status, UserListItem? user)
    {
        Success = success;
        Status = status;
        User = user;
    }

    public static UserOperationResult SuccessResult(UserListItem user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserOperationResult(true, UserOperationResultStatus.Success, user);
    }

    public static UserOperationResult Failure(UserOperationResultStatus status)
    {
        if (status == UserOperationResultStatus.Success)
        {
            throw new ArgumentException(
                "Success requiere un UserListItem; use UserOperationResult.SuccessResult(...).", nameof(status));
        }

        return new UserOperationResult(false, status, null);
    }
}
