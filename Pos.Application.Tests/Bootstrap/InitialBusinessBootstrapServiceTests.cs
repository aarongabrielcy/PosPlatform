using Pos.Application.Bootstrap;
using Pos.Application.Tests.Common.Time;
using Pos.Domain.Branches;
using Pos.Domain.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Registers;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Bootstrap;

public class InitialBusinessBootstrapServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // ---------- A. Instalación nueva ----------

    [Fact]
    public async Task ValidRequestOnEmptyInstallationCreatesFiveAggregatesWithSingleHashAndSingleCommit()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var result = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(InitialBusinessBootstrapStatus.Created, result.Status);
        Assert.NotNull(result.OrganizationId);
        Assert.NotNull(result.BranchId);
        Assert.NotNull(result.RegisterId);
        Assert.NotNull(result.RoleId);
        Assert.NotNull(result.UserId);

        Assert.Equal(1, orgRepo.AddCallCount);
        Assert.Equal(1, branchRepo.AddCallCount);
        Assert.Equal(1, registerRepo.AddCallCount);
        Assert.Equal(1, roleRepo.AddCallCount);
        Assert.Equal(1, userRepo.AddCallCount);
        Assert.Equal(1, hasher.HashCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);

        var organizations = await orgRepo.GetAllAsync(CancellationToken.None);
        var organization = Assert.Single(organizations);
        Assert.Equal(result.OrganizationId!.Value, organization.Id);
        Assert.Equal(FixedNow, organization.CreatedAtUtc);

        var branches = await branchRepo.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        var branch = Assert.Single(branches);
        Assert.Equal(result.BranchId!.Value, branch.Id);
        Assert.Equal(organization.Id, branch.OrganizationId);
        Assert.Equal(FixedNow, branch.CreatedAtUtc);

        var registers = await registerRepo.GetByBranchAsync(branch.Id, CancellationToken.None);
        var register = Assert.Single(registers);
        Assert.Equal(result.RegisterId!.Value, register.Id);
        Assert.Equal(branch.Id, register.BranchId);
        Assert.Equal(FixedNow, register.CreatedAtUtc);

        var roles = await roleRepo.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        var role = Assert.Single(roles);
        Assert.Equal(result.RoleId!.Value, role.Id);
        Assert.Equal(FixedNow, role.CreatedAtUtc);
        Assert.Equal(
            Enum.GetValues<Permission>().OrderBy(p => p),
            role.Permissions.OrderBy(p => p));

        var users = await userRepo.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        var user = Assert.Single(users);
        Assert.Equal(result.UserId!.Value, user.Id);
        Assert.Equal(role.Id, user.RoleId);
        Assert.Equal(FixedNow, user.CreatedAtUtc);
        Assert.Equal("hashed:SuperSecret123", user.PasswordHash.Value);
    }

    // ---------- B. AlreadyInitialized completo ----------

    [Fact]
    public async Task CompleteExistingInstallationReturnsAlreadyInitializedWithoutWriting()
    {
        var (organization, branch, register, role, user) = SeedCompleteInstallation();

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository([role]);
        var userRepo = new FakeUserRepository([user]);
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var result = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(InitialBusinessBootstrapStatus.AlreadyInitialized, result.Status);
        Assert.Null(result.OrganizationId);
        Assert.Null(result.BranchId);
        Assert.Null(result.RegisterId);
        Assert.Null(result.RoleId);
        Assert.Null(result.UserId);

        Assert.Equal(0, hasher.HashCallCount);
        Assert.Equal(0, orgRepo.AddCallCount);
        Assert.Equal(0, branchRepo.AddCallCount);
        Assert.Equal(0, registerRepo.AddCallCount);
        Assert.Equal(0, roleRepo.AddCallCount);
        Assert.Equal(0, userRepo.AddCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
    }

    // ---------- C. Más de una Organization ----------

    [Fact]
    public async Task MultipleOrganizationsThrowsStateExceptionWithoutWriting()
    {
        var organizationA = new Organization(OrganizationId.New(), "Organization A", FixedNow);
        var organizationB = new Organization(OrganizationId.New(), "Organization B", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organizationA, organizationB]);
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    // ---------- D. Estados parciales ----------

    [Fact]
    public async Task OrganizationWithoutBranchThrowsStateException()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    [Fact]
    public async Task BranchWithoutRegisterThrowsStateException()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    [Fact]
    public async Task OrganizationWithoutAdministrativeRoleThrowsStateException()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);
        var register = new Register(RegisterId.New(), branch.Id, "Register 1", "MAIN", FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    [Fact]
    public async Task AdministrativeRoleWithoutUserThrowsStateException()
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
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    [Fact]
    public async Task RoleMissingOneAdministrativePermissionIsNotConsideredAdministrative()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var branch = new Branch(BranchId.New(), organization.Id, "Main Branch", "MAIN", FixedNow);
        var register = new Register(RegisterId.New(), branch.Id, "Register 1", "MAIN", FixedNow);
        var incompletePermissions = Enum.GetValues<Permission>().Where(p => p != Permission.ManageRoles);
        var role = new Role(RoleId.New(), organization.Id, "Manager", FixedNow, incompletePermissions);
        var user = new User(
            UserId.New(), organization.Id, role.Id, "manager", "Manager", new PasswordHash("hash"), FixedNow);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var branchRepo = new FakeBranchRepository([branch]);
        var registerRepo = new FakeRegisterRepository([register]);
        var roleRepo = new FakeRoleRepository([role]);
        var userRepo = new FakeUserRepository([user]);
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    // ---------- E. Validación de entrada ----------

    [Fact]
    public async Task NullRequestThrowsDomainValidationExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.BootstrapAsync(null!, CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
        Assert.Equal(0, hasher.HashCallCount);
    }

    [Theory]
    [InlineData("", "Main Branch", "Register 1", "admin", "Administrator", "SuperSecret123")]
    [InlineData("   ", "Main Branch", "Register 1", "admin", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "", "Register 1", "admin", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "   ", "Register 1", "admin", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "", "admin", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "   ", "admin", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "   ", "Administrator", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "admin", "", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "admin", "   ", "SuperSecret123")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "admin", "Administrator", "")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "admin", "Administrator", "   ")]
    [InlineData("Acme Retail", "Main Branch", "Register 1", "admin", "Administrator", "Sh0rt12")]
    public async Task InvalidRequestFieldsThrowDomainValidationExceptionWithoutConsultingRepositoriesOrHashing(
        string organizationName,
        string branchName,
        string registerName,
        string administratorUsername,
        string administratorDisplayName,
        string administratorPassword)
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var request = new InitialBusinessBootstrapRequest(
            organizationName, branchName, registerName, administratorUsername, administratorDisplayName, administratorPassword);

        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.BootstrapAsync(request, CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
        Assert.Equal(0, hasher.HashCallCount);
    }

    [Fact]
    public async Task PasswordLongerThanMaximumThrowsDomainValidationExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var request = ValidRequest() with { AdministratorPassword = new string('a', 257) };

        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.BootstrapAsync(request, CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
        Assert.Equal(0, hasher.HashCallCount);
    }

    // ---------- F. Fallo Domain antes del guardado ----------

    [Fact]
    public async Task DomainRejectionDuringAggregateCreationDoesNotCommitOrAddAnything()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        // Pasa la validación básica de Application (no es whitespace) pero viola la regla de
        // Domain: Organization.Name exige entre 2 y 120 caracteres. El servicio traduce el
        // rechazo de Domain a InitialBusinessBootstrapValidationException para que los
        // consumidores (incluida la UI) no necesiten depender de Pos.Domain.
        var request = ValidRequest() with { OrganizationName = "A" };

        var thrown = await Assert.ThrowsAsync<InitialBusinessBootstrapValidationException>(
            () => service.BootstrapAsync(request, CancellationToken.None));

        Assert.IsType<DomainValidationException>(thrown.InnerException);
        Assert.DoesNotContain("SuperSecret123", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", thrown.Message, StringComparison.OrdinalIgnoreCase);

        AssertNothingWasWritten(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
    }

    [Fact]
    public async Task DomainRejectionOfAdministratorUsernameAfterHashingStillAddsNothing()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        // Falla en la construcción de User, después de calcular el hash: Domain exige un
        // Username de entre 3 y 40 caracteres.
        var request = ValidRequest() with { AdministratorUsername = "ab" };

        var thrown = await Assert.ThrowsAsync<InitialBusinessBootstrapValidationException>(
            () => service.BootstrapAsync(request, CancellationToken.None));

        Assert.IsType<DomainValidationException>(thrown.InnerException);
        Assert.DoesNotContain("SuperSecret123", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("hash", thrown.Message, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(0, orgRepo.AddCallCount);
        Assert.Equal(0, branchRepo.AddCallCount);
        Assert.Equal(0, registerRepo.AddCallCount);
        Assert.Equal(0, roleRepo.AddCallCount);
        Assert.Equal(0, userRepo.AddCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
    }

    // ---------- G. Fallo SaveChanges ----------

    [Fact]
    public async Task CommitFailurePropagatesExceptionAndDoesNotReturnCreated()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var causeException = new InvalidOperationException("Fallo simulado de SaveChanges.");
        var unitOfWork = new FakeUnitOfWork(_ => throw causeException);
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        Assert.Same(causeException, thrown);
        Assert.Equal(1, unitOfWork.CommitCallCount);
        Assert.Equal(1, hasher.HashCallCount);
    }

    // ---------- H. Cancelación ----------

    [Fact]
    public async Task CancellationWhileWaitingForTheLockThrowsAndDoesNotLeaveTheLockStuck()
    {
        var releaseFirstCall = new TaskCompletionSource();
        var firstCallStarted = new TaskCompletionSource();

        var orgRepo = new FakeOrganizationRepository
        {
            BeforeGetAllAsync = async () =>
            {
                firstCallStarted.TrySetResult();
                await releaseFirstCall.Task;
            },
        };
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();

        var service1 = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
        var firstCallTask = service1.BootstrapAsync(ValidRequest(), CancellationToken.None);

        await firstCallStarted.Task;

        using var cts = new CancellationTokenSource();
        var service2 = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
        var secondCallTask = service2.BootstrapAsync(ValidRequest(), cts.Token);
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => secondCallTask);

        releaseFirstCall.SetResult();
        var firstResult = await firstCallTask;
        Assert.Equal(InitialBusinessBootstrapStatus.Created, firstResult.Status);

        var service3 = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
        var thirdResult = await service3.BootstrapAsync(ValidRequest(), CancellationToken.None);
        Assert.Equal(InitialBusinessBootstrapStatus.AlreadyInitialized, thirdResult.Status);
    }

    // ---------- I. Idempotencia ----------

    [Fact]
    public async Task SecondCallAfterCreationIsIdempotentWithoutDuplicatesOrExtraCommits()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();
        var service = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var firstResult = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);
        var secondResult = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(InitialBusinessBootstrapStatus.Created, firstResult.Status);
        Assert.Equal(InitialBusinessBootstrapStatus.AlreadyInitialized, secondResult.Status);

        Assert.Equal(1, orgRepo.AddCallCount);
        Assert.Equal(1, branchRepo.AddCallCount);
        Assert.Equal(1, registerRepo.AddCallCount);
        Assert.Equal(1, roleRepo.AddCallCount);
        Assert.Equal(1, userRepo.AddCallCount);
        Assert.Equal(1, hasher.HashCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);
    }

    // ---------- J. Concurrencia ----------

    [Fact]
    public async Task TwoConcurrentCallsProduceExactlyOneCreatedAndOneAlreadyInitialized()
    {
        var orgRepo = new FakeOrganizationRepository();
        var branchRepo = new FakeBranchRepository();
        var registerRepo = new FakeRegisterRepository();
        var roleRepo = new FakeRoleRepository();
        var userRepo = new FakeUserRepository();
        var unitOfWork = new FakeUnitOfWork();
        var hasher = new FakePasswordHasher();

        var service1 = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);
        var service2 = BuildService(orgRepo, branchRepo, registerRepo, roleRepo, userRepo, unitOfWork, hasher);

        var task1 = service1.BootstrapAsync(ValidRequest(), CancellationToken.None);
        var task2 = service2.BootstrapAsync(ValidRequest(), CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        Assert.Single(results, r => r.Status == InitialBusinessBootstrapStatus.Created);
        Assert.Single(results, r => r.Status == InitialBusinessBootstrapStatus.AlreadyInitialized);

        Assert.Equal(1, hasher.HashCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);
        Assert.Equal(1, orgRepo.AddCallCount);
        Assert.Equal(1, branchRepo.AddCallCount);
        Assert.Equal(1, registerRepo.AddCallCount);
        Assert.Equal(1, roleRepo.AddCallCount);
        Assert.Equal(1, userRepo.AddCallCount);
    }

    // ---------- Helpers ----------

    private static InitialBusinessBootstrapService BuildService(
        FakeOrganizationRepository organizationRepository,
        FakeBranchRepository branchRepository,
        FakeRegisterRepository registerRepository,
        FakeRoleRepository roleRepository,
        FakeUserRepository userRepository,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher hasher) =>
        new(
            organizationRepository,
            branchRepository,
            registerRepository,
            roleRepository,
            userRepository,
            unitOfWork,
            new FakeClock(FixedNow),
            hasher);

    private static InitialBusinessBootstrapRequest ValidRequest() => new(
        "Acme Retail",
        "Main Branch",
        "Register 1",
        "admin",
        "Administrator",
        "SuperSecret123");

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

    private static void AssertNothingWasWritten(
        FakeOrganizationRepository orgRepo,
        FakeBranchRepository branchRepo,
        FakeRegisterRepository registerRepo,
        FakeRoleRepository roleRepo,
        FakeUserRepository userRepo,
        FakeUnitOfWork unitOfWork,
        FakePasswordHasher hasher)
    {
        Assert.Equal(0, orgRepo.AddCallCount);
        Assert.Equal(0, branchRepo.AddCallCount);
        Assert.Equal(0, registerRepo.AddCallCount);
        Assert.Equal(0, roleRepo.AddCallCount);
        Assert.Equal(0, userRepo.AddCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
        Assert.Equal(0, hasher.HashCallCount);
    }
}
