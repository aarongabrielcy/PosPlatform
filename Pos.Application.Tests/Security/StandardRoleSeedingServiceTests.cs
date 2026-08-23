using Pos.Application.Security;
using Pos.Application.Tests.Bootstrap;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Security;

public class StandardRoleSeedingServiceTests
{
    private static readonly DateTimeOffset UtcNow = DateTimeOffset.UtcNow;

    private static Organization CreateOrganization() =>
        new(Domain.Common.Identifiers.OrganizationId.New(), "Tienda Uno", UtcNow);

    [Fact]
    public async Task SeedsBothManagerAndCashierForANewlyBootstrappedInstallation()
    {
        var organization = CreateOrganization();
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var roles = await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.Contains(roles, r => r.Name == "Manager");
        Assert.Contains(roles, r => r.Name == "Cashier");
    }

    [Fact]
    public async Task SeedsBothRolesForAnExistingInstallationThatOnlyHadAdministrator()
    {
        var organization = CreateOrganization();
        var administratorRole = new Role(
            RoleId.New(), organization.Id, "Administrator", UtcNow, Enum.GetValues<Permission>());
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([administratorRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var roles = await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.Equal(3, roles.Count);
        Assert.Contains(roles, r => r.Name == "Administrator");
        Assert.Contains(roles, r => r.Name == "Manager");
        Assert.Contains(roles, r => r.Name == "Cashier");
    }

    // Los Roles ya están EXACTAMENTE en el conjunto canónico vigente (StandardRoles.*Permissions()):
    // ningún GrantPermission/RevokePermission tiene efecto, así que no debe haber ni creación ni
    // UpdateAsync/Commit. La idempotencia con permisos DESACTUALIZADOS (el caso real de una
    // instalación existente) se prueba por separado en ReconciliationUpgrades*.
    [Fact]
    public async Task IsIdempotentWhenBothRolesAlreadyExistWithTheCurrentCanonicalPermissions()
    {
        var organization = CreateOrganization();
        // StandardRoles es internal a Pos.Application (sin InternalsVisibleTo hacia este proyecto de
        // pruebas): se listan los mismos permisos vigentes de forma literal, igual criterio que
        // OldManagerPermissions/OldCashierPermissions más abajo.
        var managerRole = new Role(
            RoleId.New(), organization.Id, "Manager", UtcNow,
            [
                Permission.ProcessSale, Permission.ApplyDiscount, Permission.CancelSale, Permission.ProcessReturn,
                Permission.OpenCashDrawer, Permission.OpenRegisterSession, Permission.CloseRegisterSession,
                Permission.ViewCashTotals, Permission.ManageCashMovements, Permission.ManageProducts,
                Permission.AdjustInventory, Permission.ViewReports, Permission.ViewProductAudit,
                Permission.ViewSalesHistory, Permission.ViewProducts, Permission.ViewInventory,
                Permission.ReprintReceipt,
            ]);
        var cashierRole = new Role(
            RoleId.New(), organization.Id, "Cashier", UtcNow,
            [
                Permission.ProcessSale, Permission.OpenCashDrawer, Permission.OpenRegisterSession,
                Permission.CloseRegisterSession, Permission.ViewSalesHistory, Permission.ViewProducts,
                Permission.ViewInventory, Permission.ReprintReceipt,
            ]);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([managerRole, cashierRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        Assert.Equal(0, roleRepository.AddCallCount);
        Assert.Equal(0, roleRepository.UpdateCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
    }

    // ---------- READ-ONLY CORRECTION: reconciliación de permisos (sección 18-21/27 de la tarea) ----------

    // Simula una instalación ya sembrada ANTES de esta corrección (Manager/Cashier con el conjunto
    // de permisos viejo, sin ViewSalesHistory/ViewProducts/ViewInventory, y un Administrator
    // sembrado con solo los Permission que existían en ese momento, sin los tres nuevos). Prueba
    // exactamente el escenario descrito en la tarea: "existing Cashier role created with OLD
    // permission set -> reconciliation runs -> same RoleId -> new read permissions present ->
    // forbidden mutation permissions still absent".
    private static readonly Permission[] OldManagerPermissions =
    [
        Permission.ProcessSale, Permission.ApplyDiscount, Permission.CancelSale, Permission.ProcessReturn,
        Permission.OpenCashDrawer, Permission.OpenRegisterSession, Permission.CloseRegisterSession,
        Permission.ViewCashTotals, Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewReports,
        Permission.ViewProductAudit,
    ];

    private static readonly Permission[] OldCashierPermissions =
    [
        Permission.ProcessSale, Permission.OpenCashDrawer, Permission.OpenRegisterSession, Permission.CloseRegisterSession,
    ];

    private static readonly Permission[] OldAdministratorPermissions =
    [
        Permission.ProcessSale, Permission.ApplyDiscount, Permission.CancelSale, Permission.ProcessReturn,
        Permission.OpenCashDrawer, Permission.OpenRegisterSession, Permission.CloseRegisterSession,
        Permission.ViewCashTotals, Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewReports,
        Permission.ManageUsers, Permission.ManageRoles, Permission.ViewProductAudit,
    ];

    [Fact]
    public async Task ReconciliationUpgradesAnExistingCashierRoleWithTheOldPermissionSetPreservingRoleId()
    {
        var organization = CreateOrganization();
        var cashierRoleId = RoleId.New();
        var cashierRole = new Role(cashierRoleId, organization.Id, "Cashier", UtcNow, OldCashierPermissions);
        // Manager también ya existe (con su propio permiso viejo) para que
        // EnsureStandardRolesExistAsync solo reconcilie, sin crear ningún Role nuevo en esta prueba.
        var managerRole = new Role(RoleId.New(), organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([cashierRole, managerRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciled = await roleRepository.GetByIdAsync(cashierRoleId, CancellationToken.None);
        Assert.NotNull(reconciled);
        Assert.Equal(cashierRoleId, reconciled!.Id);
        Assert.True(reconciled.HasPermission(Permission.ViewSalesHistory));
        Assert.True(reconciled.HasPermission(Permission.ViewProducts));
        Assert.True(reconciled.HasPermission(Permission.ViewInventory));
        // Los permisos de escritura prohibidos para Cashier siguen ausentes tras la reconciliación.
        Assert.False(reconciled.HasPermission(Permission.ManageProducts));
        Assert.False(reconciled.HasPermission(Permission.AdjustInventory));
        Assert.False(reconciled.HasPermission(Permission.ManageUsers));
        Assert.Equal(0, roleRepository.AddCallCount);
        Assert.Equal(2, roleRepository.UpdateCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task ReconciliationUpgradesAnExistingManagerRoleWithTheOldPermissionSetPreservingRoleId()
    {
        var organization = CreateOrganization();
        var managerRoleId = RoleId.New();
        var managerRole = new Role(managerRoleId, organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var cashierRole = new Role(RoleId.New(), organization.Id, "Cashier", UtcNow, OldCashierPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([managerRole, cashierRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciled = await roleRepository.GetByIdAsync(managerRoleId, CancellationToken.None);
        Assert.NotNull(reconciled);
        Assert.Equal(managerRoleId, reconciled!.Id);
        Assert.True(reconciled.HasPermission(Permission.ViewSalesHistory));
        Assert.True(reconciled.HasPermission(Permission.ViewProducts));
        Assert.True(reconciled.HasPermission(Permission.ViewInventory));
        // Manager conserva su administración previa: la reconciliación nunca revoca permisos que
        // el conjunto canónico vigente sigue incluyendo.
        Assert.True(reconciled.HasPermission(Permission.ManageProducts));
        Assert.True(reconciled.HasPermission(Permission.AdjustInventory));
        Assert.False(reconciled.HasPermission(Permission.ManageUsers));
    }

    // Segunda corrida sobre un Role ya reconciliado: no debe haber ningún cambio adicional
    // (sección 27 de la tarea: "Second run: idempotent, no duplicates, no unnecessary changes").
    [Fact]
    public async Task ReconciliationIsIdempotentOnASecondRun()
    {
        var organization = CreateOrganization();
        var cashierRole = new Role(RoleId.New(), organization.Id, "Cashier", UtcNow, OldCashierPermissions);
        var managerRole = new Role(RoleId.New(), organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([cashierRole, managerRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);
        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        Assert.Equal(0, roleRepository.AddCallCount);
        Assert.Equal(2, roleRepository.UpdateCallCount);
        Assert.Equal(1, unitOfWork.CommitCallCount);
    }

    // Escenario crítico (sección 18/19 de la tarea): sin esta reconciliación, un Administrator ya
    // persistido antes de agregar un Permission nuevo deja de "contener todos los valores de
    // Permission", lo que rompe InstallationStructureInspector.SatisfiesAdministrativePermissions
    // (usado tanto para el arranque como para IsTopTierRole en UserManagementService) y el arranque
    // de una instalación existente. Administrator nunca se recrea (sección 20): mismo RoleId.
    [Fact]
    public async Task ReconciliationToppsUpAnExistingAdministratorRoleMissingNewPermissionsPreservingRoleId()
    {
        var organization = CreateOrganization();
        var administratorRoleId = RoleId.New();
        var administratorRole = new Role(
            administratorRoleId, organization.Id, "Administrator", UtcNow, OldAdministratorPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([administratorRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciled = await roleRepository.GetByIdAsync(administratorRoleId, CancellationToken.None);
        Assert.NotNull(reconciled);
        Assert.Equal(administratorRoleId, reconciled!.Id);
        Assert.Equal("Administrator", reconciled.Name);
        Assert.All(Enum.GetValues<Permission>(), permission => Assert.True(reconciled.HasPermission(permission)));

        // Ahora el Role vuelve a satisfacer "todos los permisos": el arranque (GetInstallationStateAsync)
        // ya no se rompe.
        var administrativePermissions = new HashSet<Permission>(reconciled.Permissions);
        Assert.True(Enum.GetValues<Permission>().All(administrativePermissions.Contains));
    }

    [Fact]
    public async Task ReconciliationDoesNotCreateAdministratorWhenItDoesNotExistYet()
    {
        var organization = CreateOrganization();
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var roles = await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.DoesNotContain(roles, r => r.Name == "Administrator");
    }

    // Un Role personalizado del usuario (ni Administrator ni Manager ni Cashier) nunca se toca: la
    // reconciliación solo busca por esos tres nombres exactos (sección 20 de la tarea: "Do not
    // overwrite custom roles").
    [Fact]
    public async Task ReconciliationDoesNotTouchACustomRole()
    {
        var organization = CreateOrganization();
        var customRoleId = RoleId.New();
        var customRole = new Role(
            customRoleId, organization.Id, "Almacenista", UtcNow, [Permission.AdjustInventory]);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([customRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciled = await roleRepository.GetByIdAsync(customRoleId, CancellationToken.None);
        Assert.NotNull(reconciled);
        Assert.Equal("Almacenista", reconciled!.Name);
        Assert.Single(reconciled.Permissions);
        Assert.True(reconciled.HasPermission(Permission.AdjustInventory));
        Assert.Equal(0, roleRepository.UpdateCallCount);
    }

    [Fact]
    public async Task DoesNothingWhenThereIsNoSingleOrganizationYet()
    {
        var organizationRepository = new FakeOrganizationRepository();
        var roleRepository = new FakeRoleRepository();
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        Assert.Equal(0, roleRepository.AddCallCount);
        Assert.Equal(0, unitOfWork.CommitCallCount);
    }

    // ---------- Matriz de permisos congelada (sección 6/32 de la tarea) ----------

    [Fact]
    public async Task ManagerNeverReceivesUserOrRoleAdministrationPermissions()
    {
        var (roleRepository, organization) = await SeedAsync();

        var manager = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Manager");

        Assert.False(manager.HasPermission(Permission.ManageUsers));
        Assert.False(manager.HasPermission(Permission.ManageRoles));
    }

    [Fact]
    public async Task ManagerReceivesSalesRegisterProductsInventoryAndReportsPermissions()
    {
        var (roleRepository, organization) = await SeedAsync();

        var manager = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Manager");

        Assert.True(manager.HasPermission(Permission.ProcessSale));
        Assert.True(manager.HasPermission(Permission.OpenRegisterSession));
        Assert.True(manager.HasPermission(Permission.CloseRegisterSession));
        Assert.True(manager.HasPermission(Permission.ManageProducts));
        Assert.True(manager.HasPermission(Permission.AdjustInventory));
        Assert.True(manager.HasPermission(Permission.ViewReports));
    }

    // READ-ONLY CORRECTION (sección 3/17 de la tarea): Manager puede hacer todo lo que Cashier
    // puede, incluida la lectura operativa.
    [Fact]
    public async Task ManagerAlsoReceivesTheReadOnlyPermissionsCashierHas()
    {
        var (roleRepository, organization) = await SeedAsync();

        var manager = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Manager");

        Assert.True(manager.HasPermission(Permission.ViewSalesHistory));
        Assert.True(manager.HasPermission(Permission.ViewProducts));
        Assert.True(manager.HasPermission(Permission.ViewInventory));
        Assert.True(manager.HasPermission(Permission.ReprintReceipt));
    }

    [Fact]
    public async Task CashierOnlyReceivesSalesAndRegisterPermissions()
    {
        var (roleRepository, organization) = await SeedAsync();

        var cashier = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Cashier");

        Assert.True(cashier.HasPermission(Permission.ProcessSale));
        Assert.True(cashier.HasPermission(Permission.OpenRegisterSession));
        Assert.True(cashier.HasPermission(Permission.CloseRegisterSession));
    }

    // READ-ONLY CORRECTION (sección 3/6 de la tarea): READ != MODIFY - Cashier gana lectura
    // operativa de Historial/Productos/Inventario sin ganar ninguno de los permisos de escritura
    // correspondientes (probado por separado en CashierNeverReceivesProductInventoryReportsOrUserAdministrationPermissions).
    [Fact]
    public async Task CashierReceivesTheNewReadOnlyPermissions()
    {
        var (roleRepository, organization) = await SeedAsync();

        var cashier = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Cashier");

        Assert.True(cashier.HasPermission(Permission.ViewSalesHistory));
        Assert.True(cashier.HasPermission(Permission.ViewProducts));
        Assert.True(cashier.HasPermission(Permission.ViewInventory));
        Assert.True(cashier.HasPermission(Permission.ReprintReceipt));
    }

    [Fact]
    public async Task CashierNeverReceivesProductInventoryReportsOrUserAdministrationPermissions()
    {
        var (roleRepository, organization) = await SeedAsync();

        var cashier = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None))
            .Single(r => r.Name == "Cashier");

        Assert.False(cashier.HasPermission(Permission.ManageProducts));
        Assert.False(cashier.HasPermission(Permission.AdjustInventory));
        Assert.False(cashier.HasPermission(Permission.ViewReports));
        Assert.False(cashier.HasPermission(Permission.ManageUsers));
        Assert.False(cashier.HasPermission(Permission.ManageRoles));
    }

    // BASIC-CASH-01 (sección 16/32): Manager/Admin pueden registrar CashIn/CashOut, Cashier no.
    [Fact]
    public async Task ManagerReceivesManageCashMovementsButCashierDoesNot()
    {
        var (roleRepository, organization) = await SeedAsync();

        var roles = await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        var manager = roles.Single(r => r.Name == "Manager");
        var cashier = roles.Single(r => r.Name == "Cashier");

        Assert.True(manager.HasPermission(Permission.ManageCashMovements));
        Assert.False(cashier.HasPermission(Permission.ManageCashMovements));
    }

    // Reconciliación (sección 32/45): un Manager ya persistido antes de que ManageCashMovements
    // existiera debe ganarlo en el próximo arranque, conservando su RoleId; Cashier nunca lo gana.
    [Fact]
    public async Task ReconciliationAddsManageCashMovementsToAnExistingManagerButNeverToCashier()
    {
        var organization = CreateOrganization();
        var managerRoleId = RoleId.New();
        var managerRole = new Role(managerRoleId, organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var cashierRoleId = RoleId.New();
        var cashierRole = new Role(cashierRoleId, organization.Id, "Cashier", UtcNow, OldCashierPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([managerRole, cashierRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciledManager = await roleRepository.GetByIdAsync(managerRoleId, CancellationToken.None);
        var reconciledCashier = await roleRepository.GetByIdAsync(cashierRoleId, CancellationToken.None);
        Assert.Equal(managerRoleId, reconciledManager!.Id);
        Assert.True(reconciledManager.HasPermission(Permission.ManageCashMovements));
        Assert.Equal(cashierRoleId, reconciledCashier!.Id);
        Assert.False(reconciledCashier.HasPermission(Permission.ManageCashMovements));
    }

    // Administrator siempre tiene todos los permisos, incluido ManageCashMovements, vía
    // AdministrativePermissionSet.All() (sección 32).
    [Fact]
    public async Task AdministratorAlwaysHasManageCashMovements()
    {
        var organization = CreateOrganization();
        var administratorRoleId = RoleId.New();
        var administratorRole = new Role(
            administratorRoleId, organization.Id, "Administrator", UtcNow, OldAdministratorPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([administratorRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciled = await roleRepository.GetByIdAsync(administratorRoleId, CancellationToken.None);
        Assert.True(reconciled!.HasPermission(Permission.ManageCashMovements));
    }

    // BASIC-PRN-01 (sección 25/49 de la tarea): un Cashier/Manager ya persistido antes de que
    // ReprintReceipt existiera debe ganarlo en el próximo arranque, conservando su RoleId. A
    // diferencia de ManageCashMovements, AMBOS roles lo ganan (Cashier también puede reimprimir).
    [Fact]
    public async Task ReconciliationAddsReprintReceiptToBothAnExistingManagerAndCashierPreservingRoleId()
    {
        var organization = CreateOrganization();
        var managerRoleId = RoleId.New();
        var managerRole = new Role(managerRoleId, organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var cashierRoleId = RoleId.New();
        var cashierRole = new Role(cashierRoleId, organization.Id, "Cashier", UtcNow, OldCashierPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([managerRole, cashierRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciledManager = await roleRepository.GetByIdAsync(managerRoleId, CancellationToken.None);
        var reconciledCashier = await roleRepository.GetByIdAsync(cashierRoleId, CancellationToken.None);
        Assert.Equal(managerRoleId, reconciledManager!.Id);
        Assert.True(reconciledManager.HasPermission(Permission.ReprintReceipt));
        Assert.Equal(cashierRoleId, reconciledCashier!.Id);
        Assert.True(reconciledCashier.HasPermission(Permission.ReprintReceipt));

        // Segunda corrida: idempotente, sin cambios ni duplicados (sección 49 de la tarea).
        var addCallCountAfterFirstRun = roleRepository.AddCallCount;
        var roleCountAfterFirstRun = (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None)).Count;

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        Assert.Equal(addCallCountAfterFirstRun, roleRepository.AddCallCount);
        Assert.Equal(roleCountAfterFirstRun, (await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None)).Count);
    }

    // BASIC-CFG-01, sección 27/50: ManageSettings solo llega a Administrator vía
    // AdministrativePermissionSet.All(); no está en StandardRoles.ManagerPermissions()/
    // CashierPermissions(), así que Manager/Cashier nunca lo reciben.
    [Fact]
    public async Task ManagerAndCashierNeverReceiveManageSettings()
    {
        var (roleRepository, organization) = await SeedAsync();

        var roles = await roleRepository.GetByOrganizationAsync(organization.Id, CancellationToken.None);
        Assert.False(roles.Single(r => r.Name == "Manager").HasPermission(Permission.ManageSettings));
        Assert.False(roles.Single(r => r.Name == "Cashier").HasPermission(Permission.ManageSettings));
    }

    // Mismo escenario que ReconciliationToppsUpAnExistingAdministratorRoleMissingNewPermissions,
    // enfocado específicamente en ManageSettings: un Administrator ya persistido antes de agregar
    // este Permission lo gana en el próximo arranque; Manager (reconciliado en la misma corrida)
    // nunca lo gana.
    [Fact]
    public async Task ReconciliationAddsManageSettingsOnlyToAdministratorNeverToManager()
    {
        var organization = CreateOrganization();
        var administratorRoleId = RoleId.New();
        var administratorRole = new Role(
            administratorRoleId, organization.Id, "Administrator", UtcNow, OldAdministratorPermissions);
        var managerRoleId = RoleId.New();
        var managerRole = new Role(managerRoleId, organization.Id, "Manager", UtcNow, OldManagerPermissions);
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository([administratorRole, managerRole]);
        var unitOfWork = new FakeUnitOfWork();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, unitOfWork, new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        var reconciledAdmin = await roleRepository.GetByIdAsync(administratorRoleId, CancellationToken.None);
        var reconciledManager = await roleRepository.GetByIdAsync(managerRoleId, CancellationToken.None);
        Assert.True(reconciledAdmin!.HasPermission(Permission.ManageSettings));
        Assert.False(reconciledManager!.HasPermission(Permission.ManageSettings));
    }

    private static async Task<(FakeRoleRepository RoleRepository, Organization Organization)> SeedAsync()
    {
        var organization = CreateOrganization();
        var organizationRepository = new FakeOrganizationRepository([organization]);
        var roleRepository = new FakeRoleRepository();
        var service = new StandardRoleSeedingService(
            organizationRepository, roleRepository, new FakeUnitOfWork(), new Common.Time.FakeClock(UtcNow));

        await service.EnsureStandardRolesExistAsync(CancellationToken.None);

        return (roleRepository, organization);
    }
}
