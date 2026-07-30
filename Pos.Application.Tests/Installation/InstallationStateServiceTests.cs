using Pos.Application.Installation;
using Pos.Application.Tests.Bootstrap;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Registers;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Installation;

public class InstallationStateServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmptyInstallationReturnsRequiresSetup()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.RequiresSetup, state);
    }

    [Fact]
    public async Task CompleteInstallationReturnsInitialized()
    {
        var (organization, branch, register, role, user) = SeedCompleteInstallation();

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository([role]);
        var userRepo = new FakeUserRepository([user]);
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.Initialized, state);
    }

    [Fact]
    public async Task MultipleOrganizationsReturnsInvalidState()
    {
        var organizationA = new Organization(OrganizationId.New(), "Organization A", FixedNow);
        var organizationB = new Organization(OrganizationId.New(), "Organization B", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organizationA, organizationB]);
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.InvalidState, state);
    }

    [Fact]
    public async Task OrganizationWithoutBranchReturnsInvalidState()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.InvalidState, state);
    }

    [Fact]
    public async Task BranchWithoutRegisterReturnsInvalidState()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.InvalidState, state);
    }

    [Fact]
    public async Task OrganizationWithoutAdministrativeRoleReturnsInvalidState()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);
        var register = new Register(RegisterId.New(), branch.Id, "Register 1", "MAIN", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.InvalidState, state);
    }

    [Fact]
    public async Task AdministrativeRoleWithoutUserReturnsInvalidState()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);
        var register = new Register(RegisterId.New(), branch.Id, "Register 1", "MAIN", FixedNow);
        var role = new Role(RoleId.New(), organization.Id, "Administrator", FixedNow, Enum.GetValues<Permission>());

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository([role]);
        var userRepo = new FakeUserRepository();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        var state = await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(InstallationState.InvalidState, state);
    }

    [Fact]
    public async Task GetInstallationStateAsyncDoesNotWriteToAnyRepository()
    {
        var (organization, branch, register, role, user) = SeedCompleteInstallation();

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository([role]);
        var userRepo = new FakeUserRepository([user]);
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo);

        await service.GetInstallationStateAsync(CancellationToken.None);

        Assert.Equal(0, orgRepo.AddCallCount);
        Assert.Equal(0, branchRepo.AddCallCount);
        Assert.Equal(0, registerRepo.AddCallCount);
        Assert.Equal(0, roleRepo.AddCallCount);
        Assert.Equal(0, userRepo.AddCallCount);
    }

    private static InstallationStateService BuildService(
        FakeOrganizationRepository organizationRepository,
        FakeBranchRepository branchRepository,
        FakeRegisterRepository registerRepository,
        FakeRoleRepository roleRepository,
        FakeUserRepository userRepository) =>
        new(organizationRepository, branchRepository, registerRepository, roleRepository, userRepository);

    private static (Organization Organization, Branch Branch, Register Register, Role Role, User User)
        SeedCompleteInstallation()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);
        var register = new Register(RegisterId.New(), branch.Id, "Register 1", "MAIN", FixedNow);
        var role = new Role(RoleId.New(), organization.Id, "Administrator", FixedNow, Enum.GetValues<Permission>());
        var user = new User(
            UserId.New(), organization.Id, role.Id, "admin", "Administrator", new PasswordHash("hash"), FixedNow);

        return (organization, branch, register, role, user);
    }
}
