using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.ProductAudit;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;

namespace Pos.Infrastructure.Tests.Integration;

// TAREA 24E, secciones 44/45: notificación administrativa persistente contra SQLite real,
// incluyendo el caso multi-usuario (dos destinatarios con ReadAtUtc independiente por receipt) y
// la atomicidad (Product/Audit/Notification/Recipients en un único commit).
public class AdministrativeNotificationSqliteIntegrationTests
{
    private static readonly DateTimeOffset SeedTimestamp = SqliteSeedHelper.DefaultTimestamp;

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<Guid> SeedUserAsync(
        PosDbContext context, Guid organizationId, Guid roleId, string username, string displayName, bool isActive = true)
    {
        var userId = Guid.NewGuid();

        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = username,
            DisplayName = displayName,
            PasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=",
            IsActive = isActive,
            CreatedAtUtc = SeedTimestamp,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return userId;
    }

    private static async Task<Guid> SeedRoleAsync(
        PosDbContext context, Guid organizationId, string name, bool isActive, params Permission[] permissions)
    {
        var roleId = Guid.NewGuid();

        var role = new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = name,
            IsActive = isActive,
            CreatedAtUtc = SeedTimestamp,
        };

        role.Permissions.AddRange(permissions.Select(p => new RolePermissionRecord { RoleId = roleId, Permission = p.ToString() }));

        context.Add(role);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return roleId;
    }

    private static ProductManagementService BuildManagementService(
        PosDbContext context, Guid organizationId, Guid branchId, Guid userId, Guid roleId, params Permission[] permissions)
    {
        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(userId), new OrganizationId(organizationId), new RoleId(roleId),
            "ACTOR", "Actor", "Rol", permissions));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            RegisterSessionId.New(), new OrganizationId(organizationId), new BranchId(branchId), RegisterId.New(),
            "Caja 1", new UserId(userId), "Actor", SeedTimestamp, 100m, "MXN"));

        var clock = new SystemClock();

        return new ProductManagementService(
            userSession,
            registerSession,
            new EfProductRepository(context),
            new EfInventoryItemRepository(context),
            new EfInventoryMovementRepository(context),
            new EfProductCatalogQuery(context),
            new EfProductAuditRepository(context),
            new EfProductAuditQuery(context),
            new AdministrativeNotificationWriter(
                new EfAdministrativeNotificationAudienceQuery(context), new EfAdministrativeNotificationRepository(context), clock),
            new Enforcement.FakeInstallationEnforcementStateService(),
            context,
            clock);
    }

    [Fact]
    public async Task UpdatingSalePriceCreatesOneNotificationWithAnUnreadReceiptForTheAdminAndNoneForACashierWithoutPermission()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var adminRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Administrador", isActive: true,
            Permission.ManageProducts, Permission.ViewProductAudit);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var cashierRoleId = await SeedRoleAsync(context, graph.OrganizationId, "Cajero", isActive: true, Permission.ProcessSale);
        await SeedUserAsync(context, graph.OrganizationId, cashierRoleId, "CAJERO", "Cajero 02");

        var managementService = BuildManagementService(
            context, graph.OrganizationId, graph.BranchId, adminUserId, adminRoleId,
            Permission.ManageProducts, Permission.ViewProductAudit);

        var product = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(p => p.Id == graph.ProductId);

        var result = await managementService.UpdateAsync(new Pos.Application.Products.ManageProduct.UpdateProductRequest(
            new ProductId(graph.ProductId), product.Sku, product.Barcode, product.Name, product.Description,
            27.50m, product.CostAmount, null));

        Assert.True(result.Success);

        var auditEventRecord = await context.Set<ProductAuditEventRecord>().AsNoTracking()
            .SingleAsync(e => e.ProductId == graph.ProductId && e.Action == ProductAuditAction.Updated);

        var notificationRecord = await context.Set<AdministrativeNotificationRecord>().AsNoTracking().SingleAsync();
        Assert.Equal(auditEventRecord.Id, notificationRecord.ProductAuditEventId);

        var recipients = await context.Set<AdministrativeNotificationRecipientRecord>().AsNoTracking().ToListAsync();
        var adminReceipt = Assert.Single(recipients, r => r.UserId == adminUserId);
        Assert.Null(adminReceipt.ReadAtUtc);

        Assert.DoesNotContain(recipients, r => r.UserId != adminUserId);

        var notificationQuery = new EfAdministrativeNotificationQuery(context);
        var unreadCount = await notificationQuery.GetUnreadCountAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), CancellationToken.None);
        Assert.Equal(1, unreadCount);

        var page = await notificationQuery.GetForUserAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), 0, 50, CancellationToken.None);
        var item = Assert.Single(page.Items);
        Assert.False(item.IsRead);
        Assert.Equal(ProductAuditAction.Updated, item.AuditAction);

        // ---------- MarkReadAsync ----------
        var notificationRepository = new EfAdministrativeNotificationRepository(context);
        await notificationRepository.MarkReadAsync(
            new AdministrativeNotificationId(notificationRecord.Id), new UserId(adminUserId), SeedTimestamp.AddMinutes(5),
            CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);

        var afterMarkRead = await notificationQuery.GetForUserAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), 0, 50, CancellationToken.None);
        Assert.True(Assert.Single(afterMarkRead.Items).IsRead);

        var unreadCountAfterMarkRead = await notificationQuery.GetUnreadCountAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), CancellationToken.None);
        Assert.Equal(0, unreadCountAfterMarkRead);

        // La notification sigue existiendo (no desaparece por leerla, TAREA 24E, sección 23).
        var afterMarkReadPage = await notificationQuery.GetForUserAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), 0, 50, CancellationToken.None);
        Assert.Single(afterMarkReadPage.Items);
    }

    [Fact]
    public async Task UpdatingNameOnlyCreatesAnAuditEventButNoNotification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        var adminRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Administrador", isActive: true,
            Permission.ManageProducts, Permission.ViewProductAudit);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var managementService = BuildManagementService(
            context, graph.OrganizationId, graph.BranchId, adminUserId, adminRoleId,
            Permission.ManageProducts, Permission.ViewProductAudit);

        var product = await context.Set<ProductRecord>().AsNoTracking().SingleAsync(p => p.Id == graph.ProductId);

        var result = await managementService.UpdateAsync(new Pos.Application.Products.ManageProduct.UpdateProductRequest(
            new ProductId(graph.ProductId), product.Sku, product.Barcode, "Nombre actualizado", product.Description,
            product.SalePriceAmount, product.CostAmount, null));

        Assert.True(result.Success);
        Assert.Equal(1, await context.Set<ProductAuditEventRecord>().CountAsync());
        Assert.Equal(0, await context.Set<AdministrativeNotificationRecord>().CountAsync());
    }

    [Fact]
    public async Task CreatingAProductNeverCreatesANotificationEvenWithSensitiveInitialFields()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context, includeProduct: false);
        var adminRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Administrador", isActive: true,
            Permission.ManageProducts, Permission.ViewProductAudit);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var userSession = new InMemoryCurrentUserSession();
        userSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(adminUserId), new OrganizationId(graph.OrganizationId), new RoleId(adminRoleId),
            "ADMIN", "Administrador", "Administrador", [Permission.ManageProducts, Permission.ViewProductAudit]));

        var registerSession = new InMemoryCurrentRegisterSession();
        registerSession.SetActiveSession(new ActiveRegisterSession(
            RegisterSessionId.New(), new OrganizationId(graph.OrganizationId), new BranchId(graph.BranchId), RegisterId.New(),
            "Caja 1", new UserId(adminUserId), "Administrador", SeedTimestamp, 100m, "MXN"));

        var createService = new Pos.Application.Products.CreateProduct.CreateProductService(
            userSession, registerSession, new EfProductRepository(context), new EfInventoryItemRepository(context),
            new EfProductAuditRepository(context), new Enforcement.FakeInstallationEnforcementStateService(),
            context, new SystemClock());

        var result = await createService.CreateAsync(
            new Pos.Application.Products.CreateProduct.CreateProductRequest(
                "SKU-NEW", null, "Producto nuevo", null, 10m, 5m, true, 10m, 2m),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, await context.Set<ProductAuditEventRecord>().CountAsync());
        Assert.Equal(0, await context.Set<AdministrativeNotificationRecord>().CountAsync());
    }

    [Fact]
    public async Task DeactivatingAProductWithTwoEligibleRecipientsCreatesTwoIndependentReceipts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        var adminRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Administrador", isActive: true,
            Permission.ManageProducts, Permission.ViewProductAudit);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var ownerRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Dueño", isActive: true, Permission.ViewProductAudit);
        var ownerUserId = await SeedUserAsync(context, graph.OrganizationId, ownerRoleId, "OWNER", "Dueño");

        var managementService = BuildManagementService(
            context, graph.OrganizationId, graph.BranchId, adminUserId, adminRoleId,
            Permission.ManageProducts, Permission.ViewProductAudit);

        var result = await managementService.SetActiveAsync(new ProductId(graph.ProductId), false);
        Assert.True(result.Success);

        var notificationRecord = await context.Set<AdministrativeNotificationRecord>().AsNoTracking().SingleAsync();
        var recipients = await context.Set<AdministrativeNotificationRecipientRecord>().AsNoTracking()
            .Where(r => r.NotificationId == notificationRecord.Id)
            .ToListAsync();
        Assert.Equal(2, recipients.Count);

        var notificationQuery = new EfAdministrativeNotificationQuery(context);
        var notificationRepository = new EfAdministrativeNotificationRepository(context);

        // El actor (Admin) abre la notificación: solo su receipt cambia.
        await notificationRepository.MarkReadAsync(
            new AdministrativeNotificationId(notificationRecord.Id), new UserId(adminUserId), SeedTimestamp.AddMinutes(1),
            CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);

        var adminUnread = await notificationQuery.GetUnreadCountAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), CancellationToken.None);
        var ownerUnread = await notificationQuery.GetUnreadCountAsync(
            new OrganizationId(graph.OrganizationId), new UserId(ownerUserId), CancellationToken.None);

        Assert.Equal(0, adminUnread);
        Assert.Equal(1, ownerUnread);

        var adminPage = await notificationQuery.GetForUserAsync(
            new OrganizationId(graph.OrganizationId), new UserId(adminUserId), 0, 50, CancellationToken.None);
        Assert.True(Assert.Single(adminPage.Items).IsRead);

        var ownerPage = await notificationQuery.GetForUserAsync(
            new OrganizationId(graph.OrganizationId), new UserId(ownerUserId), 0, 50, CancellationToken.None);
        Assert.False(Assert.Single(ownerPage.Items).IsRead);
    }

    [Fact]
    public async Task AdjustingInventoryPersistsProductAuditAndNotificationInASingleCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);
        await SqliteSeedHelper.SeedInventoryItemAsync(context, graph.BranchId, graph.ProductId, quantity: 10m);

        var adminRoleId = await SeedRoleAsync(
            context, graph.OrganizationId, "Administrador", isActive: true,
            Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewProductAudit);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var managementService = BuildManagementService(
            context, graph.OrganizationId, graph.BranchId, adminUserId, adminRoleId,
            Permission.ManageProducts, Permission.AdjustInventory, Permission.ViewProductAudit);

        var result = await managementService.AdjustInventoryAsync(
            new Pos.Application.Products.ManageProduct.AdjustProductInventoryRequest(
                new ProductId(graph.ProductId), InventoryAdjustmentType.Decrease, 3m));

        Assert.True(result.Success);
        Assert.Equal(1, await context.Set<ProductAuditEventRecord>().CountAsync());
        Assert.Equal(1, await context.Set<AdministrativeNotificationRecord>().CountAsync());
        Assert.Equal(1, await context.Set<InventoryMovementRecord>().CountAsync());
    }

    [Fact]
    public async Task DeactivatingAProductWithNoEligibleRecipientsPersistsNoNotification()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();

        var context = CreateContext(connection);
        await using var contextDisposable = context;
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        // El único rol de la Organization no tiene ViewProductAudit: sin destinatarios elegibles.
        var adminRoleId = await SeedRoleAsync(context, graph.OrganizationId, "Administrador", isActive: true, Permission.ManageProducts);
        var adminUserId = await SeedUserAsync(context, graph.OrganizationId, adminRoleId, "ADMIN", "Administrador");

        var managementService = BuildManagementService(
            context, graph.OrganizationId, graph.BranchId, adminUserId, adminRoleId, Permission.ManageProducts);

        var result = await managementService.SetActiveAsync(new ProductId(graph.ProductId), false);

        Assert.True(result.Success);
        Assert.Equal(1, await context.Set<ProductAuditEventRecord>().CountAsync());
        Assert.Equal(0, await context.Set<AdministrativeNotificationRecord>().CountAsync());
    }
}
