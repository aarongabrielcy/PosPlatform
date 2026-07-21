using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfOrganizationRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfOrganizationRepository(context);

        var result = await repository.GetByIdAsync(OrganizationId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncReturnsExistingOrganizationPreservingIsActive()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        context.Add(new OrganizationRecord
        {
            Id = id,
            Name = "Acme",
            IsActive = false,
            CreatedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfOrganizationRepository(context);
        var organization = await repository.GetByIdAsync(new OrganizationId(id), CancellationToken.None);

        Assert.NotNull(organization);
        Assert.Equal("Acme", organization!.Name);
        Assert.False(organization.IsActive);
    }

    [Fact]
    public async Task GetByIdAsyncDoesNotTrackTheReturnedRecord()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        context.Add(new OrganizationRecord { Id = id, Name = "Acme", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfOrganizationRepository(context);
        await repository.GetByIdAsync(new OrganizationId(id), CancellationToken.None);

        Assert.Empty(context.ChangeTracker.Entries<OrganizationRecord>());
    }

    // ---------- GetFirstAsync ----------

    [Fact]
    public async Task GetFirstAsyncReturnsNullWhenDatabaseIsEmpty()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfOrganizationRepository(context);

        var result = await repository.GetFirstAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetFirstAsyncReturnsTheOnlyOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var id = Guid.NewGuid();
        context.Add(new OrganizationRecord { Id = id, Name = "Acme", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfOrganizationRepository(context);
        var result = await repository.GetFirstAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(id, result!.Id.Value);
    }

    [Fact]
    public async Task GetFirstAsyncIsDeterministicByNameThenId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var lowestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var highestId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        context.Add(new OrganizationRecord { Id = highestId, Name = "Alpha", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new OrganizationRecord { Id = lowestId, Name = "Beta", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfOrganizationRepository(context);
        var result = await repository.GetFirstAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(highestId, result!.Id.Value);
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncTracksRecordAsAdded()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organization = new Organization(OrganizationId.New(), "Acme", CreatedAtUtc);
        var repository = new EfOrganizationRepository(context);

        await repository.AddAsync(organization, CancellationToken.None);

        var entry = context.ChangeTracker.Entries<OrganizationRecord>().Single(e => e.Entity.Id == organization.Id.Value);
        Assert.Equal(EntityState.Added, entry.State);
    }

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organization = new Organization(OrganizationId.New(), "Acme", CreatedAtUtc);
        var repository = new EfOrganizationRepository(context);

        await repository.AddAsync(organization, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<OrganizationRecord>().AnyAsync(r => r.Id == organization.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncPersistsAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var organization = new Organization(OrganizationId.New(), "Acme", CreatedAtUtc);
        var repository = new EfOrganizationRepository(context);

        await repository.AddAsync(organization, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await context.Set<OrganizationRecord>().SingleAsync(r => r.Id == organization.Id.Value);
        Assert.Equal("Acme", reloaded.Name);
    }

    [Fact]
    public async Task AddAsyncRejectsNullOrganization()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfOrganizationRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfOrganizationRepository(null!));
    }
}
