using Pos.Application.Common.Persistence;
using Pos.Application.Common.Time;
using Pos.Application.Installation;
using Pos.Application.Organizations;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Security;

public sealed class StandardRoleSeedingService : IStandardRoleSeedingService
{
    // Mismo nombre que InitialBusinessBootstrapService.AdministratorRoleName (ese Role SIEMPRE lo
    // crea Bootstrap, nunca este servicio - sección 20 de la tarea: "Administrator: do not rename
    // or recreate"). Se duplica el literal en vez de exponerlo públicamente desde Bootstrap para no
    // acoplar ambos servicios: aquí solo hace falta el nombre para buscar el Role por
    // GetByNameAsync, nunca para crearlo.
    private const string AdministratorRoleName = "Administrator";

    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public StandardRoleSeedingService(
        IOrganizationRepository organizationRepository,
        IRoleRepository roleRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    // Llamado desde App.xaml.cs tanto ANTES de evaluar el estado estructural de la instalación
    // (para nivelar los permisos de Administrator/Manager/Cashier antes de que
    // InstallationStructureInspector los evalúe - ver sección 18/19 de READ-ONLY CORRECTION: un
    // Permission nuevo en el enum, sin esta reconciliación, deja al Administrator YA PERSISTIDO sin
    // "todos los permisos" y rompe el arranque) como justo antes de mostrar Login (para crear
    // Manager/Cashier si faltan, p.ej. recién saliendo de Setup). Solo actúa cuando hay exactamente
    // una Organization; en cualquier otro caso no hay nada seguro que sembrar/reconciliar todavía.
    // Idempotente: sin cambios pendientes, no hace ningún Commit.
    public async Task EnsureStandardRolesExistAsync(CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        if (organizations.Count != 1)
        {
            return;
        }

        var organizationId = organizations[0].Id;
        var now = _clock.UtcNow;
        var changed = false;

        // Administrator NUNCA se crea aquí (siempre lo crea InitialBusinessBootstrapService,
        // sección 20 de la tarea: "do not rename or recreate"): solo se nivela su conjunto de
        // permisos a AdministrativePermissionSet.All() cuando el enum Permission creció desde que
        // ese Role se persistió. Si por alguna razón no existiera todavía (Bootstrap no completado),
        // no hay nada que reconciliar.
        changed |= await ReconcileIfExistsAsync(
            organizationId, AdministratorRoleName, AdministrativePermissionSet.All(), cancellationToken);

        changed |= await EnsureRoleExistsAndReconciledAsync(
            organizationId, StandardRoles.ManagerRoleName, StandardRoles.ManagerPermissions(), now, cancellationToken);

        changed |= await EnsureRoleExistsAndReconciledAsync(
            organizationId, StandardRoles.CashierRoleName, StandardRoles.CashierPermissions(), now, cancellationToken);

        if (changed)
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }
    }

    // Crea el Role canónico si falta (igual que antes); si ya existe, lo reconcilia al conjunto de
    // permisos canónico vigente (sección 19-21 de la tarea: "existing installations that already
    // seeded Manager/Cashier must be upgraded idempotently", preservando RoleId, sin recrear el
    // usuario ni el Role).
    private async Task<bool> EnsureRoleExistsAndReconciledAsync(
        OrganizationId organizationId,
        string roleName,
        Permission[] canonicalPermissions,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByNameAsync(organizationId, roleName, cancellationToken);

        if (existing is null)
        {
            var role = new Role(RoleId.New(), organizationId, roleName, now, canonicalPermissions);

            await _roleRepository.AddAsync(role, cancellationToken);

            return true;
        }

        return await SyncPermissionsAsync(existing, canonicalPermissions, cancellationToken);
    }

    private async Task<bool> ReconcileIfExistsAsync(
        OrganizationId organizationId,
        string roleName,
        IReadOnlyCollection<Permission> canonicalPermissions,
        CancellationToken cancellationToken)
    {
        var existing = await _roleRepository.GetByNameAsync(organizationId, roleName, cancellationToken);

        return existing is not null && await SyncPermissionsAsync(existing, canonicalPermissions, cancellationToken);
    }

    // Sincroniza el Role EXACTAMENTE al conjunto canónico (agrega los que faltan, quita los que ya
    // no corresponden): mismo criterio que RoleMapper.UpdateRecord ya aplica a nivel de persistencia
    // (sección 20 de la tarea: "ensure canonical role permissions match current definition"). Nunca
    // toca un Role que no sea exactamente uno de los tres nombres canónicos (Administrator/
    // Manager/Cashier resueltos por nombre exacto vía GetByNameAsync), así que un Role personalizado
    // del usuario jamás se ve afectado (sección 20: "Do not overwrite custom roles"). Sin cambios
    // pendientes, no llama a UpdateAsync (no-op detectable para las pruebas de idempotencia).
    private async Task<bool> SyncPermissionsAsync(
        Role role, IReadOnlyCollection<Permission> canonicalPermissions, CancellationToken cancellationToken)
    {
        var desired = new HashSet<Permission>(canonicalPermissions);
        var current = new HashSet<Permission>(role.Permissions);

        if (desired.SetEquals(current))
        {
            return false;
        }

        foreach (var permission in desired.Except(current))
        {
            role.GrantPermission(permission);
        }

        foreach (var permission in current.Except(desired))
        {
            role.RevokePermission(permission);
        }

        await _roleRepository.UpdateAsync(role, cancellationToken);

        return true;
    }
}
