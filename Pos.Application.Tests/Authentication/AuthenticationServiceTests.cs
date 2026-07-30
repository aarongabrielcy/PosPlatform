using Pos.Application.Authentication;
using Pos.Application.Tests.Bootstrap;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Authentication;

public class AuthenticationServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private const string ValidPassword = "SuperSecret123";

    // ---------- A. Credenciales correctas ----------

    [Fact]
    public async Task ValidCredentialsReturnSuccessWithFullyLoadedIdentityAndEstablishSession()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.Success, result.Status);
        Assert.NotNull(result.AuthenticatedUser);
        Assert.Equal(user.Id, result.AuthenticatedUser!.UserId);
        Assert.Equal(organization.Id, result.AuthenticatedUser.OrganizationId);
        Assert.Equal(role.Id, result.AuthenticatedUser.RoleId);
        Assert.Equal(user.Username, result.AuthenticatedUser.Username);
        Assert.Equal(user.DisplayName, result.AuthenticatedUser.DisplayName);
        Assert.Equal(role.Name, result.AuthenticatedUser.RoleName);
        Assert.Equal(
            Enum.GetValues<Permission>().OrderBy(p => p),
            result.AuthenticatedUser.Permissions.OrderBy(p => p));

        Assert.Equal(1, sessionWriter.SetAuthenticatedUserCallCount);
        Assert.Same(result.AuthenticatedUser, sessionWriter.LastAuthenticatedUser);
    }

    // ---------- B. Usuario inexistente ----------

    [Fact]
    public async Task UnknownUsernameReturnsInvalidCredentialsWithoutEstablishingSession()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("ghost", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
        Assert.Null(result.AuthenticatedUser);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
        Assert.Equal(0, hasher.VerifyCallCount);
    }

    // ---------- C. Password incorrecto ----------

    [Fact]
    public async Task WrongPasswordReturnsInvalidCredentialsWithoutEstablishingSession()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", "WrongPassword1"), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
        Assert.Null(result.AuthenticatedUser);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
        Assert.Equal(1, hasher.VerifyCallCount);
    }

    // ---------- D. Usuario inactivo ----------

    [Fact]
    public async Task InactiveUserReturnsInactiveUserWithoutEstablishingSession()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        user.Deactivate();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InactiveUser, result.Status);
        Assert.Null(result.AuthenticatedUser);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
    }

    // ---------- E. Role inexistente ----------

    [Fact]
    public async Task MissingRoleReturnsInvalidInstallationStateWithoutEstablishingSession()
    {
        var (organization, _, user) = SeedActiveAdministrator();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidInstallationState, result.Status);
        Assert.Null(result.AuthenticatedUser);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
    }

    // ---------- F. Role inactivo ----------

    [Fact]
    public async Task InactiveRoleReturnsInactiveRoleWithoutEstablishingSession()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        role.Deactivate();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InactiveRole, result.Status);
        Assert.Null(result.AuthenticatedUser);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
    }

    // ---------- G. Cero Organizations ----------

    [Fact]
    public async Task NoOrganizationsReturnsInvalidInstallationState()
    {
        var orgRepo = new FakeOrganizationRepository();
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidInstallationState, result.Status);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
    }

    // ---------- H. Más de una Organization ----------

    [Fact]
    public async Task MultipleOrganizationsReturnsInvalidInstallationStateWithoutSelectingOneSilently()
    {
        var organizationA = new Organization(OrganizationId.New(), "Organization A", FixedNow);
        var organizationB = new Organization(OrganizationId.New(), "Organization B", FixedNow);
        var orgRepo = new FakeOrganizationRepository([organizationA, organizationB]);
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidInstallationState, result.Status);
        Assert.Equal(0, sessionWriter.SetAuthenticatedUserCallCount);
    }

    // ---------- I. Validación ----------

    [Fact]
    public async Task NullRequestThrowsArgumentNullExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.AuthenticateAsync(null!, CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
    }

    [Fact]
    public async Task EmptyUsernameThrowsArgumentExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AuthenticateAsync(new AuthenticationRequest("   ", ValidPassword), CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
    }

    [Fact]
    public async Task EmptyPasswordThrowsArgumentExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AuthenticateAsync(new AuthenticationRequest("admin", "   "), CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
    }

    [Fact]
    public async Task PasswordLongerThanMaximumThrowsArgumentExceptionWithoutConsultingRepositories()
    {
        var orgRepo = new FakeOrganizationRepository();
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository();
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var request = new AuthenticationRequest("admin", new string('a', 257));

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.AuthenticateAsync(request, CancellationToken.None));

        Assert.Equal(0, orgRepo.GetAllCallCount);
    }

    // ---------- J. Seguridad ----------

    [Fact]
    public async Task SuccessfulAuthenticationNeverCallsHashAndCallsVerifyExactlyOnce()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.Equal(0, hasher.HashCallCount);
        Assert.Equal(1, hasher.VerifyCallCount);
    }

    [Fact]
    public void AuthenticatedUserTypeDoesNotExposePasswordOrHash()
    {
        var properties = typeof(AuthenticatedUser).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain("PasswordHash", properties);
        Assert.DoesNotContain("Password", properties);
    }

    [Fact]
    public async Task AuthenticationResultToStringDoesNotLeakPasswordOrHash()
    {
        var (organization, role, user) = SeedActiveAdministrator();
        var orgRepo = new FakeOrganizationRepository([organization]);
        var userRepo = new FakeUserRepository([user]);
        var roleRepo = new FakeRoleRepository([role]);
        var hasher = new FakePasswordHasher();
        var sessionWriter = new FakeCurrentUserSessionWriter();
        var service = BuildService(orgRepo, userRepo, roleRepo, hasher, sessionWriter);

        var result = await service.AuthenticateAsync(new AuthenticationRequest("admin", ValidPassword), CancellationToken.None);

        Assert.DoesNotContain(ValidPassword, result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("hashed:", result.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(ValidPassword, result.AuthenticatedUser!.ToString(), StringComparison.Ordinal);
    }

    // ---------- Helpers ----------

    private static AuthenticationService BuildService(
        FakeOrganizationRepository organizationRepository,
        FakeUserRepository userRepository,
        FakeRoleRepository roleRepository,
        FakePasswordHasher hasher,
        FakeCurrentUserSessionWriter sessionWriter) =>
        new(organizationRepository, userRepository, roleRepository, hasher, sessionWriter);

    private static (Organization Organization, Role Role, User User) SeedActiveAdministrator()
    {
        var organization = new Organization(OrganizationId.New(), "Acme Retail", FixedNow);
        var role = new Role(RoleId.New(), organization.Id, "Administrator", FixedNow, Enum.GetValues<Permission>());
        var user = new User(
            UserId.New(),
            organization.Id,
            role.Id,
            "admin",
            "Administrator",
            new PasswordHash($"hashed:{ValidPassword}"),
            FixedNow);

        return (organization, role, user);
    }
}
