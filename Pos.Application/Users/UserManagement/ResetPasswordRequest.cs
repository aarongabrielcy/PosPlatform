using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

public sealed record ResetPasswordRequest(UserId UserId, string NewPassword);
