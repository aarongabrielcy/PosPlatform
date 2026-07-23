using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Bootstrap;
using Pos.Application.Common.Time;
using Pos.Domain.Security;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Security;

namespace Pos.Infrastructure.Tests.Integration;

public class InitialBusinessBootstrapIntegrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Reduce iteraciones de PBKDF2 solo para que la prueba de integración no dependa de la
    // latencia real de producción (210,000 iteraciones); el algoritmo real sigue siendo PBKDF2-HMAC-SHA256.
    private const int TestHasherIterations = 1000;

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTimeOffset UtcNow { get; }
    }

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static InitialBusinessBootstrapService CreateService(PosDbContext context) =>
        new(
            new EfOrganizationRepository(context),
            new EfBranchRepository(context),
            new EfRegisterRepository(context),
            new EfRoleRepository(context),
            new EfUserRepository(context),
            context,
            new FixedClock(FixedNow),
            new Pbkdf2PasswordHasher(TestHasherIterations));

    private static InitialBusinessBootstrapRequest ValidRequest() => new(
        "Acme Retail",
        "Main Branch",
        "Register 1",
        "admin",
        "Administrator",
        "SuperSecret123");

    // ---------- A. Primera instalación real ----------

    [Fact]
    public async Task FirstInstallationPersistsFiveConsistentRecordsWithSecurePasswordHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var service = CreateService(context);
        var result = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);

        Assert.Equal(InitialBusinessBootstrapStatus.Created, result.Status);

        context.ChangeTracker.Clear();

        Assert.Equal(1, await context.Set<OrganizationRecord>().CountAsync());
        Assert.Equal(1, await context.Set<BranchRecord>().CountAsync());
        Assert.Equal(1, await context.Set<RegisterRecord>().CountAsync());
        Assert.Equal(1, await context.Set<RoleRecord>().CountAsync());
        Assert.Equal(1, await context.Set<UserRecord>().CountAsync());

        var organization = await context.Set<OrganizationRecord>().SingleAsync();
        var branch = await context.Set<BranchRecord>().SingleAsync();
        var register = await context.Set<RegisterRecord>().SingleAsync();
        var role = await context.Set<RoleRecord>().Include(r => r.Permissions).SingleAsync();
        var user = await context.Set<UserRecord>().SingleAsync();

        Assert.Equal(result.OrganizationId!.Value.Value, organization.Id);
        Assert.Equal(organization.Id, branch.OrganizationId);
        Assert.Equal(branch.Id, register.BranchId);
        Assert.Equal(organization.Id, role.OrganizationId);
        Assert.Equal(organization.Id, user.OrganizationId);
        Assert.Equal(role.Id, user.RoleId);

        Assert.Equal(Enum.GetValues<Permission>().Length, role.Permissions.Count);

        Assert.False(string.IsNullOrWhiteSpace(user.PasswordHash));
        Assert.DoesNotContain("SuperSecret123", user.PasswordHash, StringComparison.Ordinal);

        var hasher = new Pbkdf2PasswordHasher(TestHasherIterations);
        Assert.True(hasher.Verify("SuperSecret123", user.PasswordHash));
        Assert.False(hasher.Verify("WrongPassword1", user.PasswordHash));
    }

    // ---------- B. Segunda ejecución ----------

    [Fact]
    public async Task SecondExecutionIsIdempotentAndDoesNotDuplicateRecords()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var service = CreateService(context);
        var firstResult = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);
        context.ChangeTracker.Clear();

        var secondResult = await service.BootstrapAsync(ValidRequest(), CancellationToken.None);
        context.ChangeTracker.Clear();

        Assert.Equal(InitialBusinessBootstrapStatus.Created, firstResult.Status);
        Assert.Equal(InitialBusinessBootstrapStatus.AlreadyInitialized, secondResult.Status);

        Assert.Equal(1, await context.Set<OrganizationRecord>().CountAsync());
        Assert.Equal(1, await context.Set<BranchRecord>().CountAsync());
        Assert.Equal(1, await context.Set<RegisterRecord>().CountAsync());
        Assert.Equal(1, await context.Set<RoleRecord>().CountAsync());
        Assert.Equal(1, await context.Set<UserRecord>().CountAsync());
    }

    // ---------- C. Estado parcial físico ----------

    [Fact]
    public async Task OrganizationWithoutBranchThrowsStateExceptionAndAddsNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        context.Add(new OrganizationRecord
        {
            Id = Guid.NewGuid(),
            Name = "Acme Retail",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.Set<OrganizationRecord>().CountAsync());
        Assert.Equal(0, await context.Set<BranchRecord>().CountAsync());
    }

    [Fact]
    public async Task OrganizationWithBranchWithoutRegisterThrowsStateExceptionAndAddsNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme Retail",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        context.Add(new BranchRecord
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = "Main Branch",
            Code = "MAIN",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        context.ChangeTracker.Clear();
        Assert.Equal(1, await context.Set<OrganizationRecord>().CountAsync());
        Assert.Equal(1, await context.Set<BranchRecord>().CountAsync());
        Assert.Equal(0, await context.Set<RegisterRecord>().CountAsync());
    }

    [Fact]
    public async Task FullStructureWithoutAdministrativeRoleThrowsStateExceptionAndAddsNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme Retail",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Main Branch",
            Code = "MAIN",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        context.Add(new RegisterRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = "Register 1",
            Code = "MAIN",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        context.ChangeTracker.Clear();
        Assert.Equal(0, await context.Set<RoleRecord>().CountAsync());
        Assert.Equal(0, await context.Set<UserRecord>().CountAsync());
    }

    [Fact]
    public async Task AdministrativeRoleWithoutUserThrowsStateExceptionAndAddsNothing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        context.Add(new OrganizationRecord
        {
            Id = organizationId,
            Name = "Acme Retail",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Main Branch",
            Code = "MAIN",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });
        context.Add(new RegisterRecord
        {
            Id = Guid.NewGuid(),
            BranchId = branchId,
            Name = "Register 1",
            Code = "MAIN",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        });

        var roleRecord = new RoleRecord
        {
            Id = roleId,
            OrganizationId = organizationId,
            Name = "Administrator",
            IsActive = true,
            CreatedAtUtc = FixedNow,
        };

        foreach (var permission in Enum.GetValues<Permission>())
        {
            roleRecord.Permissions.Add(new RolePermissionRecord
            {
                RoleId = roleId,
                Permission = permission.ToString(),
                Role = roleRecord,
            });
        }

        context.Add(roleRecord);

        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var service = CreateService(context);

        await Assert.ThrowsAsync<InitialBusinessBootstrapStateException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        context.ChangeTracker.Clear();
        Assert.Equal(0, await context.Set<UserRecord>().CountAsync());
    }

    // ---------- D. Atomicidad con SaveChanges real ----------

    [Fact]
    public async Task CommitFailureDueToAReadOnlyDatabaseLeavesNoRecordInAnyTable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        // Restricción real y controlada (sin tocar migraciones ni esquema): pone la conexión en
        // modo solo-lectura para que el único SaveChangesAsync del bootstrap falle genuinamente al
        // intentar escribir el grafo completo.
        await context.Database.ExecuteSqlRawAsync("PRAGMA query_only = 1;");

        var service = CreateService(context);

        await Assert.ThrowsAnyAsync<SqliteException>(
            () => service.BootstrapAsync(ValidRequest(), CancellationToken.None));

        await context.Database.ExecuteSqlRawAsync("PRAGMA query_only = 0;");
        context.ChangeTracker.Clear();

        Assert.Equal(0, await context.Set<OrganizationRecord>().CountAsync());
        Assert.Equal(0, await context.Set<BranchRecord>().CountAsync());
        Assert.Equal(0, await context.Set<RegisterRecord>().CountAsync());
        Assert.Equal(0, await context.Set<RoleRecord>().CountAsync());
        Assert.Equal(0, await context.Set<UserRecord>().CountAsync());
    }
}
