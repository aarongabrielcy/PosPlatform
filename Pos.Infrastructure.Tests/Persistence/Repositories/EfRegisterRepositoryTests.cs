using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Registers;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfRegisterRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    private static async Task<Guid> SeedBranchAsync(PosDbContext context, string name = "Sucursal Centro")
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new BranchRecord { Id = branchId, OrganizationId = organizationId, Name = name, Code = "SUC-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return branchId;
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRegisterRepository(context);

        var result = await repository.GetByIdAsync(RegisterId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsExistingRegister()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = await SeedBranchAsync(context);
        var registerId = Guid.NewGuid();
        context.Add(new RegisterRecord { Id = registerId, BranchId = branchId, Name = "Caja 1", Code = "CAJA-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfRegisterRepository(context);
        var register = await repository.GetByIdAsync(new RegisterId(registerId), CancellationToken.None);

        Assert.NotNull(register);
        Assert.Equal("Caja 1", register!.Name);
        Assert.Equal(branchId, register.BranchId.Value);
    }

    // ---------- GetByBranchAsync ----------

    [Fact]
    public async Task GetByBranchAsyncReturnsOnlyRegistersOfThatBranchInDeterministicOrder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchA = await SeedBranchAsync(context, "Sucursal A");
        var branchB = await SeedBranchAsync(context, "Sucursal B");

        context.Add(new RegisterRecord { Id = Guid.NewGuid(), BranchId = branchA, Name = "Caja Z", Code = "Z-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RegisterRecord { Id = Guid.NewGuid(), BranchId = branchA, Name = "Caja A", Code = "A-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RegisterRecord { Id = Guid.NewGuid(), BranchId = branchB, Name = "Caja B", Code = "B-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfRegisterRepository(context);
        var registers = await repository.GetByBranchAsync(new BranchId(branchA), CancellationToken.None);

        Assert.Equal(2, registers.Count);
        Assert.Equal(["Caja A", "Caja Z"], registers.Select(r => r.Name).ToArray());
        Assert.All(registers, r => Assert.Equal(branchA, r.BranchId.Value));
    }

    [Fact]
    public async Task GetByBranchAsyncDoesNotReturnRegistersFromAnotherBranch()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchA = await SeedBranchAsync(context, "Sucursal A");
        var branchB = await SeedBranchAsync(context, "Sucursal B");

        context.Add(new RegisterRecord { Id = Guid.NewGuid(), BranchId = branchB, Name = "Caja B", Code = "B", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfRegisterRepository(context);
        var registers = await repository.GetByBranchAsync(new BranchId(branchA), CancellationToken.None);

        Assert.Empty(registers);
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = await SeedBranchAsync(context);
        var register = new Register(RegisterId.New(), new BranchId(branchId), "Caja 1", "CAJA-1", CreatedAtUtc);
        var repository = new EfRegisterRepository(context);

        await repository.AddAsync(register, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<RegisterRecord>().AnyAsync(r => r.Id == register.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var branchId = await SeedBranchAsync(context);
        var register = new Register(RegisterId.New(), new BranchId(branchId), "Caja 1", "CAJA-1", CreatedAtUtc);
        var repository = new EfRegisterRepository(context);

        await repository.AddAsync(register, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<RegisterRecord>().SingleAsync(r => r.Id == register.Id.Value);
        Assert.Equal("Caja 1", reloaded.Name);
        Assert.Equal(branchId, reloaded.BranchId);
    }

    [Fact]
    public async Task AddAsyncRejectsNullRegister()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRegisterRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfRegisterRepository(null!));
    }
}
