using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;

namespace Pos.Infrastructure.Tests.Persistence;

public class AccessRegisterSessionSqliteIntegrationTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc = new(2026, 1, 1, 20, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        connection.Open();
        return connection;
    }

    private static (Guid OrganizationId, Guid BranchId, Guid RegisterId) SeedCoreCatalog(PosDbContext context)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId,
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        return (organizationId, branchId, registerId);
    }

    // ---------- Role ----------

    [Fact]
    public async Task RoleShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RoleRecord>().SingleAsync(r => r.Id == roleId);

        Assert.Equal(organizationId, reloaded.OrganizationId);
        Assert.Equal("Cajero", reloaded.Name);
        Assert.True(reloaded.IsActive);
        Assert.Equal(CreatedAtUtc, reloaded.CreatedAtUtc);
    }

    [Fact]
    public async Task RoleWithPermissionsShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();

        var role = new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        };
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "ProcessSale", Role = role });
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "ApplyDiscount", Role = role });
        context.Add(role);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RoleRecord>()
            .Include(r => r.Permissions)
            .SingleAsync(r => r.Id == roleId);

        Assert.Equal(2, reloaded.Permissions.Count);
        Assert.Contains(reloaded.Permissions, p => p.Permission == "ProcessSale");
        Assert.Contains(reloaded.Permissions, p => p.Permission == "ApplyDiscount");
    }

    [Fact]
    public async Task DeletingRoleShouldCascadeToRolePermissions()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();

        var role = new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        };
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "ProcessSale", Role = role });
        context.Add(role);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var roleToDelete = await context.Set<RoleRecord>().SingleAsync(r => r.Id == roleId);
        context.Remove(roleToDelete);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var remainingPermissions = await context.Set<RolePermissionRecord>()
            .Where(p => p.RoleId == roleId)
            .ToListAsync();

        Assert.Empty(remainingPermissions);
    }

    [Fact]
    public async Task DeletingOrganizationWithRoleShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);

        context.Add(new RoleRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var organizationToDelete = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organizationId);
        context.Remove(organizationToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    // ---------- User ----------

    [Fact]
    public async Task UserShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<UserRecord>().SingleAsync(r => r.Id == userId);

        Assert.Equal(organizationId, reloaded.OrganizationId);
        Assert.Equal(roleId, reloaded.RoleId);
        Assert.Equal("JPEREZ", reloaded.Username);
        Assert.Equal("Juan Pérez", reloaded.DisplayName);
    }

    [Fact]
    public async Task DeletingOrganizationWithUserShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var organizationToDelete = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organizationId);
        context.Remove(organizationToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingRoleWithUserShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, _) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var roleToDelete = await context.Set<RoleRecord>().SingleAsync(r => r.Id == roleId);
        context.Remove(roleToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    // ---------- RegisterSession ----------

    [Fact]
    public async Task RegisterSessionOpenShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, registerId) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = sessionId,
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "USD",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = OpenedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RegisterSessionRecord>().SingleAsync(r => r.Id == sessionId);

        Assert.Equal(registerId, reloaded.RegisterId);
        Assert.Equal(userId, reloaded.OpenedByUserId);
        Assert.Equal(RegisterSessionStatus.Open, reloaded.Status);
        Assert.Equal(100m, reloaded.OpeningFloatAmount);
        Assert.Equal("USD", reloaded.OpeningFloatCurrency);
        Assert.Equal(OpenedAtUtc, reloaded.OpenedAtUtc);
        Assert.Null(reloaded.ClosedByUserId);
        Assert.Null(reloaded.ExpectedCashAmount);
        Assert.Null(reloaded.CountedCashAmount);
        Assert.Null(reloaded.CashDifferenceAmount);
        Assert.Null(reloaded.ClosedAtUtc);
    }

    [Fact]
    public async Task RegisterSessionClosedShouldRoundTripThroughSqlite()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, registerId) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var openedByUserId = Guid.NewGuid();
        var closedByUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = openedByUserId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = closedByUserId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "MLOPEZ",
            DisplayName = "Maria Lopez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = sessionId,
            RegisterId = registerId,
            OpenedByUserId = openedByUserId,
            ClosedByUserId = closedByUserId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "USD",
            ExpectedCashAmount = 100m,
            ExpectedCashCurrency = "USD",
            CountedCashAmount = 110m,
            CountedCashCurrency = "USD",
            CashDifferenceAmount = 10m,
            CashDifferenceCurrency = "USD",
            Status = RegisterSessionStatus.Closed,
            OpenedAtUtc = OpenedAtUtc,
            ClosedAtUtc = ClosedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RegisterSessionRecord>().SingleAsync(r => r.Id == sessionId);

        Assert.Equal(RegisterSessionStatus.Closed, reloaded.Status);
        Assert.Equal(closedByUserId, reloaded.ClosedByUserId);
        Assert.Equal(100m, reloaded.ExpectedCashAmount);
        Assert.Equal("USD", reloaded.ExpectedCashCurrency);
        Assert.Equal(110m, reloaded.CountedCashAmount);
        Assert.Equal("USD", reloaded.CountedCashCurrency);
        Assert.Equal(10m, reloaded.CashDifferenceAmount);
        Assert.Equal("USD", reloaded.CashDifferenceCurrency);
        Assert.Equal(ClosedAtUtc, reloaded.ClosedAtUtc);
    }

    [Fact]
    public async Task DeletingRegisterWithSessionShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, registerId) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = Guid.NewGuid(),
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "USD",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = OpenedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var registerToDelete = await context.Set<RegisterRecord>().SingleAsync(r => r.Id == registerId);
        context.Remove(registerToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingUserWithOpenedSessionShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, registerId) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = Guid.NewGuid(),
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "USD",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = OpenedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var userToDelete = await context.Set<UserRecord>().SingleAsync(r => r.Id == userId);
        context.Remove(userToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task DeletingUserReferencedAsClosedByShouldFailDueToRestrictForeignKey()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, _, registerId) = SeedCoreCatalog(context);
        var roleId = Guid.NewGuid();
        var openedByUserId = Guid.NewGuid();
        var closedByUserId = Guid.NewGuid();

        context.Add(new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = openedByUserId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new UserRecord
        {
            Id = closedByUserId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "MLOPEZ",
            DisplayName = "Maria Lopez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = Guid.NewGuid(),
            RegisterId = registerId,
            OpenedByUserId = openedByUserId,
            ClosedByUserId = closedByUserId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "USD",
            ExpectedCashAmount = 100m,
            ExpectedCashCurrency = "USD",
            CountedCashAmount = 110m,
            CountedCashCurrency = "USD",
            CashDifferenceAmount = 10m,
            CashDifferenceCurrency = "USD",
            Status = RegisterSessionStatus.Closed,
            OpenedAtUtc = OpenedAtUtc,
            ClosedAtUtc = ClosedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var userToDelete = await context.Set<UserRecord>().SingleAsync(u => u.Id == closedByUserId);
        context.Remove(userToDelete);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    // ---------- Prueba integrada ----------

    [Fact]
    public async Task IntegratedAccessAndRegisterSessionStructureShouldRoundTripThroughSingleCommit()
    {
        using var connection = CreateOpenConnection();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterRecord
        {
            Id = registerId,
            BranchId = branchId,
            Name = "Caja 1",
            Code = "CAJA-1",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        var role = new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Cajero",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        };
        role.Permissions.Add(new RolePermissionRecord { RoleId = roleId, Permission = "ProcessSale", Role = role });
        role.Permissions.Add(
            new RolePermissionRecord { RoleId = roleId, Permission = "OpenRegisterSession", Role = role });
        context.Add(role);

        context.Add(new UserRecord
        {
            Id = userId,
            OrganizationId = organizationId,
            RoleId = roleId,
            Username = "JPEREZ",
            DisplayName = "Juan Pérez",
            PasswordHash = SyntheticPasswordHash,
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });

        context.Add(new RegisterSessionRecord
        {
            Id = sessionId,
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 500m,
            OpeningFloatCurrency = "MXN",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = OpenedAtUtc,
        });

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var role_ = await context.Set<RoleRecord>().Include(r => r.Permissions).SingleAsync(r => r.Id == roleId);
        var user = await context.Set<UserRecord>().SingleAsync(u => u.Id == userId);
        var session = await context.Set<RegisterSessionRecord>().SingleAsync(s => s.Id == sessionId);
        var register = await context.Set<RegisterRecord>().SingleAsync(r => r.Id == registerId);

        // Relaciones
        Assert.Equal(organizationId, role_.OrganizationId);
        Assert.Equal(organizationId, user.OrganizationId);
        Assert.Equal(roleId, user.RoleId);
        Assert.Equal(registerId, session.RegisterId);
        Assert.Equal(branchId, register.BranchId);

        // Permisos
        Assert.Equal(2, role_.Permissions.Count);
        Assert.Contains(role_.Permissions, p => p.Permission == "ProcessSale");
        Assert.Contains(role_.Permissions, p => p.Permission == "OpenRegisterSession");

        // Estado
        Assert.True(role_.IsActive);
        Assert.True(user.IsActive);
        Assert.Equal(RegisterSessionStatus.Open, session.Status);

        // Fechas
        Assert.Equal(CreatedAtUtc, role_.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, user.CreatedAtUtc);
        Assert.Equal(OpenedAtUtc, session.OpenedAtUtc);

        // Usuario de apertura
        Assert.Equal(userId, session.OpenedByUserId);

        // Importes y moneda
        Assert.Equal(500m, session.OpeningFloatAmount);
        Assert.Equal("MXN", session.OpeningFloatCurrency);
    }
}
