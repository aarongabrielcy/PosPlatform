using Pos.Application.Branches;
using Pos.Application.Organizations;
using Pos.Application.Registers;
using Pos.Application.Security;
using Pos.Application.Users;

namespace Pos.Application.Installation;

public sealed class InstallationStateService : IInstallationStateService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly InstallationStructureInspector _structureInspector;

    public InstallationStateService(
        IOrganizationRepository organizationRepository,
        IBranchRepository branchRepository,
        IRegisterRepository registerRepository,
        IRoleRepository roleRepository,
        IUserRepository userRepository)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));

        ArgumentNullException.ThrowIfNull(branchRepository);
        ArgumentNullException.ThrowIfNull(registerRepository);
        ArgumentNullException.ThrowIfNull(roleRepository);
        ArgumentNullException.ThrowIfNull(userRepository);

        _structureInspector = new InstallationStructureInspector(
            branchRepository, registerRepository, roleRepository, userRepository);
    }

    public async Task<InstallationState> GetInstallationStateAsync(CancellationToken cancellationToken)
    {
        var organizations = await _organizationRepository.GetAllAsync(cancellationToken);

        if (organizations.Count == 0)
        {
            return InstallationState.RequiresSetup;
        }

        if (organizations.Count > 1)
        {
            return InstallationState.InvalidState;
        }

        var status = await _structureInspector.EvaluateAsync(organizations[0].Id, cancellationToken);

        return status == OrganizationStructureStatus.Complete
            ? InstallationState.Initialized
            : InstallationState.InvalidState;
    }
}
