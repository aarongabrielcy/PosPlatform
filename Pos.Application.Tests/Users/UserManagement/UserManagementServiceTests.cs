using Pos.Application.Authentication;
using Pos.Application.Tests.Bootstrap;
using Pos.Application.Users.UserManagement;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Security;
using Pos.Domain.Users;

namespace Pos.Application.Tests.Users.UserManagement;

public class UserManagementServiceTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;
    private const string ValidPassword = "SuperSecret123";

    // ---------- Crear usuario (sección 33 de la tarea) ----------

    [Fact]
    public async Task CreateUserAsyncCreatesCashierWhenRequestedByOwner()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("cajero1", "Luis Cajero", cashierRole.Id, ValidPassword));

        Assert.True(result.Success);
        Assert.Equal(CreateUserResultStatus.Success, result.Status);
        Assert.Equal("CAJERO1", result.User!.Username);
        Assert.Equal("Cashier", result.User.RoleName);
        Assert.True(result.User.IsActive);
    }

    [Fact]
    public async Task CreateUserAsyncCreatesManagerWhenRequestedByOwner()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var managerRole = CreateManagerRole(organization.Id);
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole, managerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("gerente1", "Ana Gerente", managerRole.Id, ValidPassword));

        Assert.True(result.Success);
        Assert.Equal("Manager", result.User!.RoleName);
    }

    [Fact]
    public async Task CreateUserAsyncCreatesAdditionalOwnerAdminWhenRoleIsTopTier()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("owner2", "Segundo Owner", ownerRole.Id, ValidPassword));

        Assert.True(result.Success);
        Assert.Equal("Administrator", result.User!.RoleName);
    }

    [Theory]
    [InlineData("CAJERO1")]
    [InlineData("  cajero1  ")]
    [InlineData("Cajero1")]
    public async Task CreateUserAsyncRejectsDuplicateUsernameRegardlessOfCasingOrWhitespace(string duplicateAttempt)
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var existingCashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Primer Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, existingCashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest(duplicateAttempt, "Otro Cajero", cashierRole.Id, ValidPassword));

        Assert.False(result.Success);
        Assert.Equal(CreateUserResultStatus.DuplicateUsername, result.Status);
    }

    [Fact]
    public async Task CreateUserAsyncDeniedForManagerWithoutManageUsers()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var managerRole = CreateManagerRole(organization.Id);
        var managerUser = new User(
            UserId.New(), organization.Id, managerRole.Id, "gerente1", "Ana Gerente",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, managerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole, managerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, managerRole, managerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("cajero1", "Luis Cajero", managerRole.Id, ValidPassword));

        Assert.False(result.Success);
        Assert.Equal(CreateUserResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task CreateUserAsyncRejectsWhenNotAuthenticated()
    {
        var (organization, ownerRole, _) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var userRepo = new FakeUserRepository();
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession();
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.CreateUserAsync(
            new CreateUserRequest("cajero1", "Luis Cajero", cashierRole.Id, ValidPassword));

        Assert.False(result.Success);
        Assert.Equal(CreateUserResultStatus.NotAuthenticated, result.Status);
    }

    // ---------- Editar usuario (sección 33/17 de la tarea) ----------

    [Fact]
    public async Task UpdateUserAsyncPreservesUserId()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);
        var originalId = cashier.Id;

        var result = await service.UpdateUserAsync(
            new UpdateUserRequest(cashier.Id, "cajero1renombrado", "Luis Renombrado", cashierRole.Id));

        Assert.True(result.Success);
        Assert.Equal(originalId, result.User!.UserId);

        var stored = await userRepo.GetByIdAsync(originalId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(originalId, stored!.Id);
        Assert.Equal("CAJERO1RENOMBRADO", stored.Username);
    }

    [Fact]
    public async Task UpdateUserAsyncRejectsRenamingToAnAlreadyTakenUsername()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashierA = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Cajero A",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var cashierB = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero2", "Cajero B",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashierA, cashierB]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.UpdateUserAsync(
            new UpdateUserRequest(cashierB.Id, "cajero1", "Cajero B", cashierRole.Id));

        Assert.False(result.Success);
        Assert.Equal(UserOperationResultStatus.DuplicateUsername, result.Status);
    }

    [Fact]
    public async Task UpdateUserAsyncAllowsKeepingTheSameUsernameUnchanged()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.UpdateUserAsync(
            new UpdateUserRequest(cashier.Id, "cajero1", "Luis Cajero Actualizado", cashierRole.Id));

        Assert.True(result.Success);
        Assert.Equal("Luis Cajero Actualizado", result.User!.DisplayName);
    }

    // ---------- Activar / desactivar (sección 12/33 de la tarea) ----------

    [Fact]
    public async Task SetActiveAsyncDeactivatesAndReactivatesACashier()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var deactivated = await service.SetActiveAsync(cashier.Id, false, CancellationToken.None);
        Assert.True(deactivated.Success);
        Assert.False(deactivated.User!.IsActive);

        var reactivated = await service.SetActiveAsync(cashier.Id, true, CancellationToken.None);
        Assert.True(reactivated.Success);
        Assert.True(reactivated.User!.IsActive);
    }

    [Fact]
    public async Task DeactivatedUserFailsSubsequentLogin()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var hasher = new FakePasswordHasher();
        var service = BuildService(session, userRepo, roleRepo, hasher: hasher);

        await service.SetActiveAsync(cashier.Id, false, CancellationToken.None);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var authenticationService = new AuthenticationService(
            orgRepo, userRepo, roleRepo, hasher, new Authentication.FakeCurrentUserSessionWriter());

        var loginResult = await authenticationService.AuthenticateAsync(
            new AuthenticationRequest("cajero1", ValidPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InactiveUser, loginResult.Status);
    }

    // ---------- Última protección de Owner/Admin activo (sección 13/14/34 de la tarea) ----------

    [Fact]
    public async Task SetActiveAsyncRejectsDeactivatingTheSoleActiveOwner()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.SetActiveAsync(ownerUser.Id, false, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(UserOperationResultStatus.CannotDeactivateLastAdmin, result.Status);

        var stored = await userRepo.GetByIdAsync(ownerUser.Id, CancellationToken.None);
        Assert.True(stored!.IsActive);
    }

    [Fact]
    public async Task SetActiveAsyncAllowsDeactivatingOneOfTwoActiveOwners()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var secondOwner = new User(
            UserId.New(), organization.Id, ownerRole.Id, "owner2", "Segundo Owner",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, secondOwner]);
        var roleRepo = new FakeRoleRepository([ownerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.SetActiveAsync(secondOwner.Id, false, CancellationToken.None);

        Assert.True(result.Success);
        Assert.False(result.User!.IsActive);
    }

    [Fact]
    public async Task UpdateUserAsyncRejectsDemotingTheSoleActiveOwnerToManager()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var managerRole = CreateManagerRole(organization.Id);
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole, managerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.UpdateUserAsync(
            new UpdateUserRequest(ownerUser.Id, ownerUser.Username, ownerUser.DisplayName, managerRole.Id));

        Assert.False(result.Success);
        Assert.Equal(UserOperationResultStatus.CannotDemoteLastAdmin, result.Status);

        var stored = await userRepo.GetByIdAsync(ownerUser.Id, CancellationToken.None);
        Assert.Equal(ownerRole.Id, stored!.RoleId);
    }

    [Fact]
    public async Task UpdateUserAsyncAllowsDemotingOneOfTwoActiveOwners()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var managerRole = CreateManagerRole(organization.Id);
        var secondOwner = new User(
            UserId.New(), organization.Id, ownerRole.Id, "owner2", "Segundo Owner",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, secondOwner]);
        var roleRepo = new FakeRoleRepository([ownerRole, managerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.UpdateUserAsync(
            new UpdateUserRequest(secondOwner.Id, secondOwner.Username, secondOwner.DisplayName, managerRole.Id));

        Assert.True(result.Success);
        Assert.Equal("Manager", result.User!.RoleName);
    }

    // Corolario directo de la misma protección basada en conteo (sección 14 de la tarea: el propio
    // Owner activo intentando desactivarse/degradarse a sí mismo es exactamente el caso "último
    // Owner/Admin activo" - no requiere ninguna comprobación adicional de identidad.
    [Fact]
    public async Task SoleOwnerCannotDeactivateOrDemoteItself()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var managerRole = CreateManagerRole(organization.Id);
        var userRepo = new FakeUserRepository([ownerUser]);
        var roleRepo = new FakeRoleRepository([ownerRole, managerRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var deactivateResult = await service.SetActiveAsync(ownerUser.Id, false, CancellationToken.None);
        var demoteResult = await service.UpdateUserAsync(
            new UpdateUserRequest(ownerUser.Id, ownerUser.Username, ownerUser.DisplayName, managerRole.Id));

        Assert.Equal(UserOperationResultStatus.CannotDeactivateLastAdmin, deactivateResult.Status);
        Assert.Equal(UserOperationResultStatus.CannotDemoteLastAdmin, demoteResult.Status);
    }

    // ---------- Restablecer contraseña (sección 18/33 de la tarea) ----------

    [Fact]
    public async Task ResetPasswordAsyncChangesPasswordOldPasswordFailsAndNewPasswordSucceeds()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var hasher = new FakePasswordHasher();
        var service = BuildService(session, userRepo, roleRepo, hasher: hasher);
        const string newPassword = "AnotherSecret456";

        var resetResult = await service.ResetPasswordAsync(
            new ResetPasswordRequest(cashier.Id, newPassword), CancellationToken.None);
        Assert.True(resetResult.Success);

        var orgRepo = new FakeOrganizationRepository([organization]);
        var authenticationService = new AuthenticationService(
            orgRepo, userRepo, roleRepo, hasher, new Authentication.FakeCurrentUserSessionWriter());

        var oldPasswordLogin = await authenticationService.AuthenticateAsync(
            new AuthenticationRequest("cajero1", ValidPassword), CancellationToken.None);
        var newPasswordLogin = await authenticationService.AuthenticateAsync(
            new AuthenticationRequest("cajero1", newPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidCredentials, oldPasswordLogin.Status);
        Assert.Equal(AuthenticationStatus.Success, newPasswordLogin.Status);
    }

    [Fact]
    public async Task ResetPasswordAsyncRejectsAPasswordThatIsTooShort()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.ResetPasswordAsync(
            new ResetPasswordRequest(cashier.Id, "short"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(UserOperationResultStatus.InvalidPassword, result.Status);
    }

    // ---------- Autorización representativa (sección 26/36 de la tarea) ----------

    [Fact]
    public async Task GetUsersAsyncReturnsEmptyForCashierWithoutManageUsers()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, cashierRole, cashier) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.GetUsersAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUsersAsyncListsAllUsersForOwner()
    {
        var (organization, ownerRole, ownerUser) = SeedOwner();
        var cashierRole = CreateCashierRole(organization.Id);
        var cashier = new User(
            UserId.New(), organization.Id, cashierRole.Id, "cajero1", "Luis Cajero",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);
        var userRepo = new FakeUserRepository([ownerUser, cashier]);
        var roleRepo = new FakeRoleRepository([ownerRole, cashierRole]);
        var session = new FakeCurrentUserSession { CurrentUser = AsAuthenticatedUser(organization, ownerRole, ownerUser) };
        var service = BuildService(session, userRepo, roleRepo);

        var result = await service.GetUsersAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, u => u.UserId == cashier.Id && u.RoleName == "Cashier");
    }

    // ---------- Helpers ----------

    private static (Organization Organization, Role OwnerRole, User OwnerUser) SeedOwner()
    {
        var organization = new Organization(OrganizationId.New(), "Tienda Uno", UtcNow);
        var ownerRole = new Role(RoleId.New(), organization.Id, "Administrator", UtcNow, Enum.GetValues<Permission>());
        var ownerUser = new User(
            UserId.New(), organization.Id, ownerRole.Id, "owner1", "Owner Uno",
            new PasswordHash("hashed:" + ValidPassword), UtcNow);

        return (organization, ownerRole, ownerUser);
    }

    private static Role CreateManagerRole(OrganizationId organizationId) =>
        new(
            RoleId.New(), organizationId, "Manager", UtcNow,
            [
                Permission.ProcessSale, Permission.OpenRegisterSession, Permission.CloseRegisterSession,
                Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewReports,
            ]);

    private static Role CreateCashierRole(OrganizationId organizationId) =>
        new(
            RoleId.New(), organizationId, "Cashier", UtcNow,
            [Permission.ProcessSale, Permission.OpenRegisterSession, Permission.CloseRegisterSession]);

    private static AuthenticatedUser AsAuthenticatedUser(Organization organization, Role role, User user) =>
        new(user.Id, organization.Id, role.Id, user.Username, user.DisplayName, role.Name, role.Permissions);

    private static UserManagementService BuildService(
        FakeCurrentUserSession session,
        FakeUserRepository userRepo,
        FakeRoleRepository roleRepo,
        FakeUnitOfWork? unitOfWork = null,
        FakePasswordHasher? hasher = null) =>
        new(
            session, userRepo, roleRepo, hasher ?? new FakePasswordHasher(),
            unitOfWork ?? new FakeUnitOfWork(), new Common.Time.FakeClock(UtcNow));
}
