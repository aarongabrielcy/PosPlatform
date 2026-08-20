using Pos.Application.Users.UserManagement;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Users;

internal sealed class FakeUserManagementService : IUserManagementService
{
    public List<UserListItem> Users { get; } = [];

    public List<RoleOption> Roles { get; } = [];

    public int GetUsersCallCount { get; private set; }

    public int GetAssignableRolesCallCount { get; private set; }

    public CreateUserRequest? LastCreateRequest { get; private set; }

    public UpdateUserRequest? LastUpdateRequest { get; private set; }

    public (UserId UserId, bool IsActive)? LastSetActiveCall { get; private set; }

    public ResetPasswordRequest? LastResetPasswordRequest { get; private set; }

    public Func<CreateUserRequest, CreateUserResult>? CreateUserHandler { get; set; }

    public Func<UpdateUserRequest, UserOperationResult>? UpdateUserHandler { get; set; }

    public Func<UserId, bool, UserOperationResult>? SetActiveHandler { get; set; }

    public Func<ResetPasswordRequest, UserOperationResult>? ResetPasswordHandler { get; set; }

    public Task<IReadOnlyList<UserListItem>> GetUsersAsync(CancellationToken cancellationToken = default)
    {
        GetUsersCallCount++;

        return Task.FromResult<IReadOnlyList<UserListItem>>(Users);
    }

    public Task<IReadOnlyList<RoleOption>> GetAssignableRolesAsync(CancellationToken cancellationToken = default)
    {
        GetAssignableRolesCallCount++;

        return Task.FromResult<IReadOnlyList<RoleOption>>(Roles);
    }

    public Task<CreateUserResult> CreateUserAsync(
        CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;

        return Task.FromResult(
            CreateUserHandler?.Invoke(request)
            ?? CreateUserResult.SuccessResult(
                new UserListItem(UserId.New(), request.Username, request.DisplayName, request.RoleId, "Cashier", true)));
    }

    public Task<UserOperationResult> UpdateUserAsync(
        UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = request;

        return Task.FromResult(
            UpdateUserHandler?.Invoke(request)
            ?? UserOperationResult.SuccessResult(
                new UserListItem(request.UserId, request.Username, request.DisplayName, request.RoleId, "Cashier", true)));
    }

    public Task<UserOperationResult> SetActiveAsync(
        UserId userId, bool isActive, CancellationToken cancellationToken = default)
    {
        LastSetActiveCall = (userId, isActive);

        return Task.FromResult(
            SetActiveHandler?.Invoke(userId, isActive)
            ?? UserOperationResult.SuccessResult(new UserListItem(userId, "USER", "User", RoleId.New(), "Cashier", isActive)));
    }

    public Task<UserOperationResult> ResetPasswordAsync(
        ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        LastResetPasswordRequest = request;

        return Task.FromResult(
            ResetPasswordHandler?.Invoke(request)
            ?? UserOperationResult.SuccessResult(new UserListItem(request.UserId, "USER", "User", RoleId.New(), "Cashier", true)));
    }
}
