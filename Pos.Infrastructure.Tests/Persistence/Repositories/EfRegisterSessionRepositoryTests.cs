using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Common.Exceptions;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Domain.RegisterSessions;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfRegisterSessionRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc = new(2026, 1, 1, 18, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // RegisterSession.RegisterId/OpenedByUserId tienen FK Restrict hacia Register/User: se
    // siembra Organization -> Branch -> Register -> Role -> User antes de insertar la sesión.
    private static async Task<(Guid RegisterId, Guid UserId)> SeedRegisterAndUserAsync(
        PosDbContext context, Guid? registerId = null)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var register = registerId ?? Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new BranchRecord { Id = branchId, OrganizationId = organizationId, Name = "Sucursal Centro", Code = "SUC-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RegisterRecord { Id = register, BranchId = branchId, Name = "Caja 1", Code = "CAJA-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RoleRecord { Id = roleId, OrganizationId = organizationId, Name = "Cajero", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new UserRecord { Id = userId, OrganizationId = organizationId, RoleId = roleId, Username = "JPEREZ", DisplayName = "Juan Pérez", PasswordHash = SyntheticPasswordHash, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return (register, userId);
    }

    private static RegisterSession CreateOpenSession(RegisterId registerId, UserId userId) =>
        new(RegisterSessionId.New(), registerId, userId, new Money(100m, "MXN"), CreatedAtUtc);

    // ---------- GetByIdAsync ----------

    [Fact]
    public async Task GetByIdAsyncReturnsNullWhenNotFound()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRegisterSessionRepository(context);

        var result = await repository.GetByIdAsync(RegisterSessionId.New(), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetByIdAsyncPreservesOpenState()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));

        await repository.AddAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(RegisterSessionStatus.Open, reloaded!.Status);
        Assert.Null(reloaded.ClosedAtUtc);
    }

    [Fact]
    public async Task GetByIdAsyncPreservesClosedStateAndCloseData()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));
        session.Close(new UserId(userId), new Money(100m, "MXN"), new Money(95m, "MXN"), ClosedAtUtc);

        await repository.AddAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(RegisterSessionStatus.Closed, reloaded!.Status);
        Assert.Equal(ClosedAtUtc, reloaded.ClosedAtUtc);
        Assert.Equal(-5m, reloaded.CashDifference!.Amount);
    }

    // ---------- GetOpenByRegisterAsync ----------

    [Fact]
    public async Task GetOpenByRegisterAsyncReturnsNullWhenNoneOpen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, _) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);

        var result = await repository.GetOpenByRegisterAsync(new RegisterId(registerId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOpenByRegisterAsyncReturnsTheOpenSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));

        await repository.AddAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var result = await repository.GetOpenByRegisterAsync(new RegisterId(registerId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(session.Id.Value, result!.Id.Value);
    }

    [Fact]
    public async Task GetOpenByRegisterAsyncIgnoresClosedSessions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var closedSession = CreateOpenSession(new RegisterId(registerId), new UserId(userId));
        closedSession.Close(new UserId(userId), new Money(100m, "MXN"), new Money(100m, "MXN"), ClosedAtUtc);

        await repository.AddAsync(closedSession, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var result = await repository.GetOpenByRegisterAsync(new RegisterId(registerId), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetOpenByRegisterAsyncThrowsWhenMoreThanOneSessionIsOpen()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);

        // El Domain no permite abrir dos sesiones activas para el mismo Register a través de un
        // caso de uso normal; se insertan directamente como records para simular datos corruptos
        // o una condición de carrera histórica y así ejercitar la detección defensiva.
        context.Add(new RegisterSessionRecord
        {
            Id = Guid.NewGuid(),
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 100m,
            OpeningFloatCurrency = "MXN",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = CreatedAtUtc,
        });
        context.Add(new RegisterSessionRecord
        {
            Id = Guid.NewGuid(),
            RegisterId = registerId,
            OpenedByUserId = userId,
            OpeningFloatAmount = 50m,
            OpeningFloatCurrency = "MXN",
            Status = RegisterSessionStatus.Open,
            OpenedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var repository = new EfRegisterSessionRepository(context);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => repository.GetOpenByRegisterAsync(new RegisterId(registerId), CancellationToken.None));
    }

    // ---------- UpdateAsync ----------

    [Fact]
    public async Task UpdateAsyncPersistsTheCloseOfAPreviouslyOpenSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));

        await repository.AddAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        session.Close(new UserId(userId), new Money(100m, "MXN"), new Money(105m, "MXN"), ClosedAtUtc);
        await repository.UpdateAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.NotNull(reloaded);
        Assert.Equal(RegisterSessionStatus.Closed, reloaded!.Status);
        Assert.Equal(ClosedAtUtc, reloaded.ClosedAtUtc);
        Assert.Equal(userId, reloaded.ClosedByUserId!.Value.Value);
        Assert.Equal(5m, reloaded.CashDifference!.Amount);
    }

    [Fact]
    public async Task UpdateAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));

        await repository.AddAsync(session, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        session.Close(new UserId(userId), new Money(100m, "MXN"), new Money(100m, "MXN"), ClosedAtUtc);
        await repository.UpdateAsync(session, CancellationToken.None);
        context.ChangeTracker.Clear();

        var reloaded = await repository.GetByIdAsync(session.Id, CancellationToken.None);

        Assert.Equal(RegisterSessionStatus.Open, reloaded!.Status);
    }

    [Fact]
    public async Task UpdateAsyncThrowsWhenTheSessionDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var repository = new EfRegisterSessionRepository(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));
        session.Close(new UserId(userId), new Money(100m, "MXN"), new Money(100m, "MXN"), ClosedAtUtc);

        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => repository.UpdateAsync(session, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsyncRejectsNullSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRegisterSessionRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.UpdateAsync(null!, CancellationToken.None));
    }

    // ---------- AddAsync ----------

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerId, userId) = await SeedRegisterAndUserAsync(context);
        var session = CreateOpenSession(new RegisterId(registerId), new UserId(userId));
        var repository = new EfRegisterSessionRepository(context);

        await repository.AddAsync(session, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<RegisterSessionRecord>().AnyAsync(r => r.Id == session.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncRejectsNullSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfRegisterSessionRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfRegisterSessionRepository(null!));
    }
}
