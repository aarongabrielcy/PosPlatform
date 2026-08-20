using Pos.Domain.Common.Identifiers;

namespace Pos.Application.Users.UserManagement;

// Rol asignable desde Crear/Editar usuario (solo Roles activos de la Organization actual).
public sealed record RoleOption(RoleId RoleId, string Name);
