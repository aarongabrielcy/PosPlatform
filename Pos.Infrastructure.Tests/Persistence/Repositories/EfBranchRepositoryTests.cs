using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Branches;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfBranchRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<Guid> SeedOrganizationAsync(PosDbContext context, Guid? id = null, string name = "Acme")
    {
        var organizationId = id ?? Guid.NewGuid();
        context.Add(new OrganizationRecord { Id = organizationId, Name = name, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return organizationId;
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfBranchRepository(context);

        var result = await repository.GetByIdAsync(BranchId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsExistingBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var branchId = Guid.NewGuid();
        context.Add(new BranchRecord
        {
            Id = branchId,
            OrganizationId = organizationId,
            Name = "Sucursal Centro",
            Code = "SUC-1",
            IsActive = true,
            CreatedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfBranchRepository(context);
        var branch = await repository.GetByIdAsync(new BranchId(branchId), CancellationToken.None);

        Assert.NotNull(branch);
        Assert.Equal("Sucursal Centro", branch!.Name);
        Assert.Equal(organizationId, branch.OrganizationId.Value);
    }

    // ---------- GetByOrganizationAsync ----------

    [Fact]
    public async Task GetByOrganizationAsyncReturnsOnlyBranchesOfThatOrganizationInDeterministicOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, name: "A");
        var organizationB = await SeedOrganizationAsync(context, name: "B");

        context.Add(new BranchRecord { Id = Guid.NewGuid(), OrganizationId = organizationA, Name = "Zeta", Code = "Z-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new BranchRecord { Id = Guid.NewGuid(), OrganizationId = organizationA, Name = "Alfa", Code = "A-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new BranchRecord { Id = Guid.NewGuid(), OrganizationId = organizationB, Name = "Beta", Code = "B-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfBranchRepository(context);
        var branches = await repository.GetByOrganizationAsync(new OrganizationId(organizationA), CancellationToken.None);

        Assert.Equal(2, branches.Count);
        Assert.Equal(["Alfa", "Zeta"], branches.Select(b => b.Name).ToArray());
        Assert.All(branches, b => Assert.Equal(organizationA, b.OrganizationId.Value));
    }

    [Fact]
    public async Task GetByOrganizationAsyncDoesNotReturnBranchesFromAnotherOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationA = await SeedOrganizationAsync(context, name: "A");
        var organizationB = await SeedOrganizationAsync(context, name: "B");

        context.Add(new BranchRecord { Id = Guid.NewGuid(), OrganizationId = organizationB, Name = "Sucursal B", Code = "B-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfBranchRepository(context);
        var branches = await repository.GetByOrganizationAsync(new OrganizationId(organizationA), CancellationToken.None);

        Assert.Empty(branches);
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
        var branch = new Branch(BranchId.New(), new OrganizationId(organizationId), "Sucursal Centro", "SUC-1", CreatedAtUtc);
        var repository = new EfBranchRepository(context);

        await repository.AddAsync(branch, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<BranchRecord>().AnyAsync(r => r.Id == branch.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organizationId = await SeedOrganizationAsync(context);
        var branch = new Branch(BranchId.New(), new OrganizationId(organizationId), "Sucursal Centro", "SUC-1", CreatedAtUtc);
        var repository = new EfBranchRepository(context);

        await repository.AddAsync(branch, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<BranchRecord>().SingleAsync(r => r.Id == branch.Id.Value);
        Assert.Equal("Sucursal Centro", reloaded.Name);
        Assert.Equal(organizationId, reloaded.OrganizationId);
    }

    [Fact]
    public async Task AddAsyncRejectsNullBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfBranchRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfBranchRepository(null!));
    }
}
