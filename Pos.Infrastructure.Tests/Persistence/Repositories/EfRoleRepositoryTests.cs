using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfRoleRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<Guid> SeedOrganizationAsync(PosDbContext context, string name = "Acme")
    {
        var organizationId = Guid.NewGuid();
        context.Add(new OrganizationRecord { Id = organizationId, Name = name, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return organizationId;
    }

    private static Role CreateRole(OrganizationId organizationId, string name, params Permission[] permissions) =>
        new(RoleId.New(), organizationId, name, CreatedAtUtc, permissions);

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRoleRepository(context);

        var result = await repository.GetByIdAsync(RoleId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncLoadsAllPermissions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfRoleRepository(context);
        var role = CreateRole(new OrganizationId(organizationId), "Cajero", Permission.ProcessSale, Permission.CancelSale, Permission.OpenCashDrawer);

        await repository.AddAsync(role, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(role.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(3, reloaded!.Permissions.Count);
        Assert.Contains(Permission.ProcessSale, reloaded.Permissions);
        Assert.Contains(Permission.CancelSale, reloaded.Permissions);
        Assert.Contains(Permission.OpenCashDrawer, reloaded.Permissions);
    }

    [Fact]
    public async Task GetByIdAsyncDoesNotReturnAnIncompleteRoleWhenThereAreNoPermissions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var repository = new EfRoleRepository(context);
        var role = CreateRole(new OrganizationId(organizationId), "SinPermisos");

        await repository.AddAsync(role, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(role.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Empty(reloaded!.Permissions);
    }

    // ---------- GetByNameAsync ----------

    [Fact]
    public async Task GetByNameAsyncFiltersByOrganizationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfRoleRepository(context);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationA), "Cajero", Permission.ProcessSale), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetByNameAsync(new OrganizationId(organizationA), "Cajero", CancellationToken.None);
        var foundForB = await repository.GetByNameAsync(new OrganizationId(organizationB), "Cajero", CancellationToken.None);

        Assert.NotNull(foundForA);
        Assert.Null(foundForB);
    }

    [Fact]
    public async Task GetByNameAsyncAllowsSameNameInDifferentOrganizations()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, "A");
        var organizationB = await SeedOrganizationAsync(context, "B");

        var repository = new EfRoleRepository(context);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationA), "Cajero", Permission.ProcessSale), CancellationToken.None);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationB), "Cajero", Permission.ManageUsers), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var foundForA = await repository.GetByNameAsync(new OrganizationId(organizationA), "Cajero", CancellationToken.None);
        var foundForB = await repository.GetByNameAsync(new OrganizationId(organizationB), "Cajero", CancellationToken.None);

        Assert.Contains(Permission.ProcessSale, foundForA!.Permissions);
        Assert.Contains(Permission.ManageUsers, foundForB!.Permissions);
    }

    // ---------- GetByOrganizationAsync ----------

    [Fact]
    public async Task GetByOrganizationAsyncLoadsFullPermissionsForEveryRoleInDeterministicOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var organizationOther = await SeedOrganizationAsync(context, "Other");

        var repository = new EfRoleRepository(context);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationId), "Zeta", Permission.ViewReports), CancellationToken.None);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationId), "Alfa", Permission.ProcessSale, Permission.CancelSale), CancellationToken.None);
        await repository.AddAsync(CreateRole(new OrganizationId(organizationOther), "Beta", Permission.ManageRoles), CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var roles = await repository.GetByOrganizationAsync(new OrganizationId(organizationId), CancellationToken.None);

        Assert.Equal(2, roles.Count);
        Assert.Equal(["Alfa", "Zeta"], roles.Select(r => r.Name).ToArray());
        Assert.Equal(2, roles.Single(r => r.Name == "Alfa").Permissions.Count);
        Assert.Single(roles.Single(r => r.Name == "Zeta").Permissions);
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var role = CreateRole(new OrganizationId(organizationId), "Cajero", Permission.ProcessSale);
        var repository = new EfRoleRepository(context);

        await repository.AddAsync(role, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<RoleRecord>().AnyAsync(r => r.Id == role.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncRejectsNullRole()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRoleRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfRoleRepository(null!));
    }
}
