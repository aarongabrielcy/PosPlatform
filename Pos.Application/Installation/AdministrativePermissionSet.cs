using Pos.Domain.Security;

namespace Pos.Application.Installation;

// Único punto de verdad para el conjunto de permisos que define un Role administrativo:
// lo usa tanto la creación del Role inicial (Bootstrap) como la evaluación estructural
// (Installation) para decidir si un Role existente satisface el requisito administrativo.
internal static class AdministrativePermissionSet
{
    public static Permission[] All() => Enum.GetValues<Permission>().OrderBy(permission => permission).ToArray();
}
