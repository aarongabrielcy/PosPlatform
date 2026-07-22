using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Users;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfUserRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private static readonly PasswordHash SyntheticPasswordHash =
        new("v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=");

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<(Guid OrganizationId, Guid RoleId)> SeedOrganizationAndRoleAsync(
        PosDbContext context, string organizationName = "Acme")
    {
        var organizationId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        context.Add(new OrganizationRecord { Id = organizationId, Name = organizationName, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RoleRecord { Id = roleId, OrganizationId = organizationId, Name = "Cajero", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return (organizationId, roleId);
    }

    private static User CreateUser(OrganizationId organizationId, RoleId roleId, string username, string displayName = "Usuario") =>
        new(UserId.New(), organizationId, roleId, username, displayName, SyntheticPasswordHash, CreatedAtUtc);

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfUserRepository(context);

        var result = await repository.GetByIdAsync(UserId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncPreservesRoleIdAndIsActive()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var repository = new EfUserRepository(context);
        var user = CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ");
        user.Deactivate();

        await repository.AddAsync(user, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(roleId, reloaded!.RoleId.Value);
        Assert.False(reloaded.IsActive);
    }

    [Fact]
    public async Task GetByIdAsyncRehydratesPasswordHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var repository = new EfUserRepository(context);
        var user = CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ");

        await repository.AddAsync(user, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(user.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(SyntheticPasswordHash, reloaded!.PasswordHash);
    }

    // ---------- GetByUsernameAsync ----------

    [Fact]
    public async Task GetByUsernameAsyncFiltersByOrganizationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationA, roleA) = await SeedOrganizationAndRoleAsync(context, "A");
        var (organizationB, _) = await SeedOrganizationAndRoleAsync(context, "B");

        var repository = new EfUserRepository(context);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationA), new RoleId(roleA), "JPEREZ"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        // El username persistido ya está normalizado en mayúsculas por User.NormalizeUsername.
        var foundForA = await repository.GetByUsernameAsync(new OrganizationId(organizationA), "JPEREZ", CancellationToken.None);
        var foundForB = await repository.GetByUsernameAsync(new OrganizationId(organizationB), "JPEREZ", CancellationToken.None);

        Assert.NotNull(foundForA);
        Assert.Null(foundForB);
    }

    [Fact]
    public async Task GetByUsernameAsyncAllowsSameUsernameInDifferentOrganizations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationA, roleA) = await SeedOrganizationAndRoleAsync(context, "A");
        var (organizationB, roleB) = await SeedOrganizationAndRoleAsync(context, "B");

        var repository = new EfUserRepository(context);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationA), new RoleId(roleA), "JPEREZ", "Juan A"), CancellationToken.None);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationB), new RoleId(roleB), "JPEREZ", "Juan B"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetByUsernameAsync(new OrganizationId(organizationA), "JPEREZ", CancellationToken.None);
        var foundForB = await repository.GetByUsernameAsync(new OrganizationId(organizationB), "JPEREZ", CancellationToken.None);

        Assert.Equal("Juan A", foundForA!.DisplayName);
        Assert.Equal("Juan B", foundForB!.DisplayName);
    }

    [Fact]
    public async Task GetByUsernameAsyncRehydratesPasswordHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var repository = new EfUserRepository(context);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var found = await repository.GetByUsernameAsync(new OrganizationId(organizationId), "JPEREZ", CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(SyntheticPasswordHash, found!.PasswordHash);
    }

    // ---------- GetByOrganizationAsync ----------

    [Fact]
    public async Task GetByOrganizationAsyncReturnsOnlyUsersOfThatOrganizationInDeterministicOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationA, roleA) = await SeedOrganizationAndRoleAsync(context, "A");
        var (organizationB, roleB) = await SeedOrganizationAndRoleAsync(context, "B");

        var repository = new EfUserRepository(context);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationA), new RoleId(roleA), "ZUSER"), CancellationToken.None);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationA), new RoleId(roleA), "AUSER"), CancellationToken.None);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationB), new RoleId(roleB), "BUSER"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var users = await repository.GetByOrganizationAsync(new OrganizationId(organizationA), CancellationToken.None);

        Assert.Equal(2, users.Count);
        Assert.Equal(["AUSER", "ZUSER"], users.Select(u => u.Username).ToArray());
    }

    [Fact]
    public async Task GetByOrganizationAsyncRehydratesPasswordHash()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var repository = new EfUserRepository(context);
        await repository.AddAsync(CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ"), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var users = await repository.GetByOrganizationAsync(new OrganizationId(organizationId), CancellationToken.None);

        Assert.Equal(SyntheticPasswordHash, Assert.Single(users).PasswordHash);
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var user = CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ");
        var repository = new EfUserRepository(context);

        await repository.AddAsync(user, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<UserRecord>().AnyAsync(r => r.Id == user.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsPasswordHashAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (organizationId, roleId) = await SeedOrganizationAndRoleAsync(context);
        var user = CreateUser(new OrganizationId(organizationId), new RoleId(roleId), "JPEREZ");
        var repository = new EfUserRepository(context);

        await repository.AddAsync(user, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var record = await context.Set<UserRecord>().SingleAsync(r => r.Id == user.Id.Value);
        Assert.Equal(SyntheticPasswordHash.Value, record.PasswordHash);
    }

    [Fact]
    public async Task AddAsyncRejectsNullUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfUserRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfUserRepository(null!));
    }
}
