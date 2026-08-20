using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

public sealed record CreateUserRequest(string Username, string DisplayName, RoleId RoleId, string Password);
