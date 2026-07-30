using Pos.Application.Branches;
using Pos.Application.Registers;
using Pos.Application.Security;
using Pos.Application.Users;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Installation;

// Lógica estructural compartida entre InitialBusinessBootstrapService (Bootstrap) e
// IInstallationStateService (Installation): evalúa, sin escribir nada, si una Organization ya
// tiene Branch, Register, Role administrativo y User administrador. Ambos consumidores traducen
// este resultado a su propia semántica (excepción vs. estado de instalación).
internal sealed class InstallationStructureInspector
{
    private readonly IBranchRepository _branchRepository;
    private readonly IRegisterRepository _registerRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRepository _userRepository;

    public InstallationStructureInspector(
        IBranchRepository branchRepository,
        IRegisterRepository registerRepository,
        IRoleRepository roleRepository,
        IUserRepository userRepository)
    {
        _branchRepository = branchRepository ?? throw new ArgumentNullException(nameof(branchRepository));
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
        _roleRepository = roleRepository ?? throw new ArgumentNullException(nameof(roleRepository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public async Task<OrganizationStructureStatus> EvaluateAsync(
        OrganizationId organizationId, CancellationToken cancellationToken)
    {
        var branches = await _branchRepository.GetByOrganizationAsync(organizationId, cancellationToken);

        if (branches.Count == 0)
        {
            return OrganizationStructureStatus.MissingBranch;
        }

        var hasBranchWithRegister = false;

        foreach (var branch in branches)
        {
            var registers = await _registerRepository.GetByBranchAsync(branch.Id, cancellationToken);

            if (registers.Count > 0)
            {
                hasBranchWithRegister = true;
                break;
            }
        }

        if (!hasBranchWithRegister)
        {
            return OrganizationStructureStatus.MissingRegisterInAnyBranch;
        }

        var roles = await _roleRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var administrativePermissions = AdministrativePermissionSet.All();

        var administrativeRoleIds = roles
            .Where(role => SatisfiesAdministrativePermissions(role, administrativePermissions))
            .Select(role => role.Id)
            .ToHashSet();

        if (administrativeRoleIds.Count == 0)
        {
            return OrganizationStructureStatus.MissingAdministrativeRole;
        }

        var users = await _userRepository.GetByOrganizationAsync(organizationId, cancellationToken);
        var hasAdministratorUser = users.Any(user => administrativeRoleIds.Contains(user.RoleId));

        return hasAdministratorUser
            ? OrganizationStructureStatus.Complete
            : OrganizationStructureStatus.MissingAdministratorUser;
    }

    private static bool SatisfiesAdministrativePermissions(
        Role role, IReadOnlyCollection<Permission> administrativePermissions)
    {
        var rolePermissions = new HashSet<Permission>(role.Permissions);

        return administrativePermissions.All(rolePermissions.Contains);
    }
}
