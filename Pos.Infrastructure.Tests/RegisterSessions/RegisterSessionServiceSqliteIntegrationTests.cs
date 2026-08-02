using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.RegisterSessions;
using Pos.Domain.Registers;
using Pos.Domain.Sales;
using Pos.Domain.Security;
using Pos.Domain.Users;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.RegisterSessions;
using Pos.Infrastructure.Tests.Persistence;
using Pos.Infrastructure.Time;
using RegisterSessionStatus = Pos.Application.RegisterSessions.RegisterSessionStatus;

namespace Pos.Infrastructure.Tests.RegisterSessions;

// Integración real contra SQLite en memoria (nunca la base real): cubre bootstrap -> login ->
// abrir caja -> reiniciar la sesión de caja en memoria (simula un cierre inesperado del proceso)
// -> detectar la caja abierta desde la base -> cerrar caja, verificando en cada paso el estado
// persistido y que login/logout no modifican RegisterSession.
public class RegisterSessionServiceSqliteIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    [Fact]
    public async Task FullOpenAndCloseFlowRoundTripsThroughSqliteAndSurvivesAnInMemoryReset()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        var (organizationId, roleId, userId) = await SeedInstallationAsync(connection);

        var currentUserSession = new InMemoryCurrentUserSession();
        var authenticatedUser = new AuthenticatedUser(
            new UserId(userId), new OrganizationId(organizationId), new RoleId(roleId), "CAJERO", "Cajero Uno",
            "Cajero", [Permission.OpenRegisterSession, Permission.CloseRegisterSession]);
        currentUserSession.SetAuthenticatedUser(authenticatedUser);

        // ---------- Login no crea ni modifica RegisterSession ----------

        await using (var context = CreateContext(connection))
        {
            Assert.Equal(0, await context.Set<RegisterSessionRecord>().CountAsync());
        }

        // ---------- Sin caja abierta ----------

        var firstRegisterSession = new InMemoryCurrentRegisterSession();

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, firstRegisterSession);
            var status = await service.GetCurrentAsync();

            Assert.Equal(RegisterSessionStatus.NoneOpen, status.Status);
        }

        // ---------- Abrir caja ----------

        RegisterSessionResult openResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, firstRegisterSession);
            openResult = await service.OpenAsync(new OpenRegisterSessionRequest(null, 500m));
        }

        Assert.True(openResult.Success);
        Assert.True(firstRegisterSession.IsOpen);

        await using (var context = CreateContext(connection))
        {
            var records = await context.Set<RegisterSessionRecord>().ToListAsync();

            var record = Assert.Single(records);
            Assert.Equal(Pos.Domain.RegisterSessions.RegisterSessionStatus.Open, record.Status);
            Assert.Equal(500m, record.OpeningFloatAmount);
            Assert.Equal("MXN", record.OpeningFloatCurrency);
            Assert.Equal(userId, record.OpenedByUserId);
        }

        // ---------- No permite una segunda apertura mientras la primera sigue abierta ----------

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, firstRegisterSession);
            var secondOpenResult = await service.OpenAsync(new OpenRegisterSessionRequest(null, 100m));

            Assert.Equal(RegisterSessionResultStatus.AlreadyOpen, secondOpenResult.Status);
        }

        await using (var context = CreateContext(connection))
        {
            Assert.Equal(1, await context.Set<RegisterSessionRecord>().CountAsync());
        }

        // ---------- La sesión de caja en memoria se reinicia (cierre inesperado del proceso) ----------

        var recoveredRegisterSession = new InMemoryCurrentRegisterSession();
        Assert.False(recoveredRegisterSession.IsOpen);

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, recoveredRegisterSession);
            var status = await service.GetCurrentAsync();

            Assert.Equal(RegisterSessionStatus.Open, status.Status);
            Assert.Equal(500m, status.ActiveSession!.OpeningAmount);
        }

        Assert.True(recoveredRegisterSession.IsOpen);

        // ---------- Cerrar caja ----------

        RegisterSessionResult closeResult;

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, recoveredRegisterSession);
            closeResult = await service.CloseAsync(new CloseRegisterSessionRequest(510m));
        }

        Assert.True(closeResult.Success);
        Assert.Equal(10m, closeResult.Summary!.Difference);
        Assert.False(recoveredRegisterSession.IsOpen);

        await using (var context = CreateContext(connection))
        {
            var record = await context.Set<RegisterSessionRecord>().SingleAsync();

            Assert.Equal(Pos.Domain.RegisterSessions.RegisterSessionStatus.Closed, record.Status);
            Assert.NotNull(record.ClosedAtUtc);
            Assert.Equal(userId, record.ClosedByUserId);
            Assert.Equal(510m, record.CountedCashAmount);
            Assert.Equal(10m, record.CashDifferenceAmount);
        }

        // ---------- No queda una segunda sesión abierta ----------

        await using (var context = CreateContext(connection))
        {
            var service = BuildService(context, currentUserSession, new InMemoryCurrentRegisterSession());
            var status = await service.GetCurrentAsync();

            Assert.Equal(RegisterSessionStatus.NoneOpen, status.Status);
        }

        // ---------- Logout no modifica RegisterSession ----------

        currentUserSession.Clear();

        await using (var context = CreateContext(connection))
        {
            Assert.Equal(1, await context.Set<RegisterSessionRecord>().CountAsync());
        }
    }

    // TAREA 25A sección 29: ExpectedCash = OpeningFloat + ventas en efectivo completadas de esta
    // sesión de caja. Siembra dos Sales Completed en efectivo directamente vía SaleRecord (no pasa
    // por CheckoutService: solo se ejercita el cálculo de RegisterSessionService.CloseAsync).
    [Fact]
    public async Task ClosingWithCompletedCashSalesAddsThemToExpectedCash()
    {
        using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();

        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var graph = await SqliteSeedHelper.SeedFullCatalogGraphAsync(context);

        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId,
            quantity: 2m, unitPriceAmount: 10m, paymentAmount: 20m,
            status: SaleStatus.Completed, completedAtUtc: SqliteSeedHelper.DefaultTimestamp);

        await SqliteSeedHelper.SeedSaleAsync(
            context, graph.OrganizationId, graph.BranchId, graph.RegisterSessionId, graph.UserId, graph.ProductId,
            saleId: Guid.NewGuid(), saleLineId: Guid.NewGuid(), paymentId: Guid.NewGuid(),
            quantity: 1m, unitPriceAmount: 10m, paymentAmount: 10m,
            status: SaleStatus.Completed, completedAtUtc: SqliteSeedHelper.DefaultTimestamp);

        context.ChangeTracker.Clear();

        var currentUserSession = new InMemoryCurrentUserSession();
        currentUserSession.SetAuthenticatedUser(new AuthenticatedUser(
            new UserId(graph.UserId), new OrganizationId(graph.OrganizationId), new RoleId(graph.RoleId),
            "JPEREZ", "Juan Pérez", "Cajero", [Permission.CloseRegisterSession]));

        var currentRegisterSession = new InMemoryCurrentRegisterSession();
        currentRegisterSession.SetActiveSession(new ActiveRegisterSession(
            new RegisterSessionId(graph.RegisterSessionId),
            new OrganizationId(graph.OrganizationId),
            new BranchId(graph.BranchId),
            new RegisterId(graph.RegisterId),
            "Caja 1",
            new UserId(graph.UserId),
            "Juan Pérez",
            SqliteSeedHelper.DefaultTimestamp,
            100m,
            "MXN"));

        var service = BuildService(context, currentUserSession, currentRegisterSession);

        // OpeningFloat sembrado por SeedFullCatalogGraphAsync = 100m; cash sales = 20m + 10m.
        var result = await service.CloseAsync(new CloseRegisterSessionRequest(130m));

        Assert.True(result.Success);
        Assert.Equal(130m, result.Summary!.ExpectedAmount);
        Assert.Equal(0m, result.Summary.Difference);
    }

    private static async Task<(Guid OrganizationId, Guid RoleId, Guid UserId)> SeedInstallationAsync(
        SqliteConnection connection)
    {
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId, Name = "Acme Retail", IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new BranchRecord
        {
            Id = branchId, OrganizationId = organizationId, Name = "Sucursal Centro", Code = "SUC-1",
            IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId, BranchId = branchId, Name = "Caja 1", Code = "CAJA-1",
            IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });

        var role = new RoleRecord
        {
            Id = roleId, OrganizationId = organizationId, Name = "Cajero", IsActive = true, CreatedAtUtc = CreatedAtUtc,
        };
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "OpenRegisterSession", Role = role });
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "CloseRegisterSession", Role = role });
        context.Add(role);

        context.Add(new UserRecord
        {
            Id = userId, OrganizationId = organizationId, RoleId = roleId, Username = "CAJERO",
            DisplayName = "Cajero Uno", PasswordHash = SyntheticPasswordHash, IsActive = true, CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return (organizationId, roleId, userId);
    }

    private static RegisterSessionService BuildService(
        PosDbContext context, ICurrentUserSession currentUserSession, ICurrentRegisterSession currentRegisterSession) =>
        new(
            currentUserSession,
            currentRegisterSession,
            new EfOrganizationRepository(context),
            new EfBranchRepository(context),
            new EfRegisterRepository(context),
            new EfRegisterSessionRepository(context),
            new EfUserRepository(context),
            new EfSaleRepository(context),
            context,
            new SystemClock());

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }
}
