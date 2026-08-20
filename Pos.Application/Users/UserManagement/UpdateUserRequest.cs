using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

public sealed record UpdateUserRequest(UserId UserId, string Username, string DisplayName, RoleId RoleId);
