namespace Pos.Application.Security;

// BASIC-USR-01: garantiza que los Roles Manager/Cashier existan para la Organization instalada,
// tanto en una instalación nueva (Setup ya creó Administrator) como en una instalación existente
// que se actualiza a esta versión (solo tenía Administrator hasta ahora). READ-ONLY CORRECTION:
// además reconcilia los permisos de los Roles canónicos ya existentes (Administrator/Manager/
// Cashier) contra su definición vigente, para que una instalación ya sembrada reciba permisos
// nuevos del enum Permission sin migración ni recreación. Idempotente: no hace ningún cambio si
// los Roles ya existen con el conjunto de permisos vigente. Ver StandardRoles para el porqué de no
// crear/renombrar Administrator.
public interface IStandardRoleSeedingService
{
    Task EnsureStandardRolesExistAsync(CancellationToken cancellationToken);
}
