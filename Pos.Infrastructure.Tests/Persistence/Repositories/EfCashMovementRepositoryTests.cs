using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Common.ValueObjects;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;

namespace Pos.Infrastructure.Tests.Persistence.Repositories;

public class EfCashMovementRepositoryTests
{
    private static readonly DateTimeOffset CreatedAtUtc = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    // Hash sintético únicamente para satisfacer la invariante Domain; no es un hash PBKDF2 real
    // y no debe usarse para autenticación.
    private const string SyntheticPasswordHash = "v1$pbkdf2-sha256$210000$c2FsdC1zeW50aGV0aWM=$aGFzaC1zeW50aGV0aWM=";

    private static PosDbContext CreateContext(SqliteConnection connection)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PosDbContext>();
        optionsBuilder.UseSqlite(connection);

        return new PosDbContext(optionsBuilder.Options);
    }

    // CashMovement.RegisterSessionId/ActorUserId tienen FK Restrict: se siembra Organization ->
    // Branch -> Register -> RegisterSession abierta -> Role -> User antes de insertar el movimiento.
    private static async Task<(Guid RegisterSessionId, Guid UserId)> SeedOpenSessionAndUserAsync(PosDbContext context)
    {
        var organizationId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var registerSessionId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Add(new OrganizationRecord { Id = organizationId, Name = "Acme", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new BranchRecord { Id = branchId, OrganizationId = organizationId, Name = "Sucursal Centro", Code = "SUC-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RegisterRecord { Id = registerId, BranchId = branchId, Name = "Caja 1", Code = "CAJA-1", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RoleRecord { Id = roleId, OrganizationId = organizationId, Name = "Gerente", IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new UserRecord { Id = userId, OrganizationId = organizationId, RoleId = roleId, Username = "GERENTE", DisplayName = "Gerente Uno", PasswordHash = SyntheticPasswordHash, IsActive = true, CreatedAtUtc = CreatedAtUtc });
        context.Add(new RegisterSessionRecord
        {
            Id = registerSessionId, RegisterId = registerId, OpenedByUserId = userId,
            OpeningFloatAmount = 100m, OpeningFloatCurrency = "MXN",
            Status = Domain.RegisterSessions.RegisterSessionStatus.Open, OpenedAtUtc = CreatedAtUtc,
        });
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        return (registerSessionId, userId);
    }

    // ---------- AddAsync / persistencia (sección 46) ----------

    [Fact]
    public async Task AddAsyncPersistsTheMovementAfterCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerSessionId, userId) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);
        var movement = CashMovement.CreateCashIn(
            CashMovementId.New(), new RegisterSessionId(registerSessionId), new UserId(userId),
            new Money(150m, "MXN"), "Reposición de efectivo", CreatedAtUtc);

        await repository.AddAsync(movement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var movements = await repository.GetByRegisterSessionAsync(
            new RegisterSessionId(registerSessionId), CancellationToken.None);

        var reloaded = Assert.Single(movements);
        Assert.Equal(movement.Id, reloaded.Id);
        Assert.Equal(CashMovementType.CashIn, reloaded.Type);
        Assert.Equal(150m, reloaded.Amount.Amount);
        Assert.Equal("MXN", reloaded.Amount.Currency);
        Assert.Equal("Reposición de efectivo", reloaded.Reason);
        Assert.Equal(userId, reloaded.ActorUserId.Value);
        Assert.Equal(CreatedAtUtc, reloaded.CreatedAtUtc);
    }

    [Fact]
    public async Task AddAsyncDoesNotPersistBeforeCommit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerSessionId, userId) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);
        var movement = CashMovement.CreateCashIn(
            CashMovementId.New(), new RegisterSessionId(registerSessionId), new UserId(userId),
            new Money(150m, "MXN"), "Reposición de efectivo", CreatedAtUtc);

        await repository.AddAsync(movement, CancellationToken.None);
        context.ChangeTracker.Clear();

        var exists = await context.Set<CashMovementRecord>().AnyAsync(r => r.Id == movement.Id.Value);
        Assert.False(exists);
    }

    [Fact]
    public async Task AddAsyncRejectsNullMovement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var repository = new EfCashMovementRepository(context);

        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    // Confirma la FK Restrict hacia register_sessions: un RegisterSessionId inexistente debe
    // fallar al hacer Commit, nunca insertarse huérfano (sección 30 de la tarea).
    [Fact]
    public async Task AddAsyncFailsOnCommitWhenRegisterSessionDoesNotExist()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (_, userId) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);
        var movement = CashMovement.CreateCashIn(
            CashMovementId.New(), RegisterSessionId.New(), new UserId(userId), new Money(10m, "MXN"), "Motivo", CreatedAtUtc);

        await repository.AddAsync(movement, CancellationToken.None);

        await Assert.ThrowsAsync<DbUpdateException>(() => context.CommitAsync(CancellationToken.None));
    }

    // ---------- GetByRegisterSessionAsync ----------

    [Fact]
    public async Task GetByRegisterSessionAsyncReturnsOnlyMovementsForThatSessionOrderedByCreatedAt()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerSessionId, userId) = await SeedOpenSessionAndUserAsync(context);
        var (otherSessionId, otherUserId) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);

        var first = CashMovement.CreateCashIn(
            CashMovementId.New(), new RegisterSessionId(registerSessionId), new UserId(userId),
            new Money(50m, "MXN"), "Primero", CreatedAtUtc);
        var second = CashMovement.CreateCashOut(
            CashMovementId.New(), new RegisterSessionId(registerSessionId), new UserId(userId),
            new Money(20m, "MXN"), "Segundo", CreatedAtUtc.AddMinutes(5));
        var otherSessionMovement = CashMovement.CreateCashIn(
            CashMovementId.New(), new RegisterSessionId(otherSessionId), new UserId(otherUserId),
            new Money(999m, "MXN"), "De otra sesión", CreatedAtUtc);

        await repository.AddAsync(second, CancellationToken.None);
        await repository.AddAsync(first, CancellationToken.None);
        await repository.AddAsync(otherSessionMovement, CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var movements = await repository.GetByRegisterSessionAsync(
            new RegisterSessionId(registerSessionId), CancellationToken.None);

        Assert.Equal(2, movements.Count);
        Assert.Equal(first.Id, movements[0].Id);
        Assert.Equal(second.Id, movements[1].Id);
    }

    // ---------- Sumas por tipo (sección 13/30) ----------

    [Fact]
    public async Task GetCashInAndCashOutTotalsSumOnlyMovementsOfTheMatchingTypeForThatSession()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerSessionId, userId) = await SeedOpenSessionAndUserAsync(context);
        var (otherSessionId, otherUserId) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);
        var sessionId = new RegisterSessionId(registerSessionId);
        var actorId = new UserId(userId);

        await repository.AddAsync(
            CashMovement.CreateCashIn(CashMovementId.New(), sessionId, actorId, new Money(100m, "MXN"), "In 1", CreatedAtUtc),
            CancellationToken.None);
        await repository.AddAsync(
            CashMovement.CreateCashIn(CashMovementId.New(), sessionId, actorId, new Money(50m, "MXN"), "In 2", CreatedAtUtc),
            CancellationToken.None);
        await repository.AddAsync(
            CashMovement.CreateCashOut(CashMovementId.New(), sessionId, actorId, new Money(30m, "MXN"), "Out 1", CreatedAtUtc),
            CancellationToken.None);
        await repository.AddAsync(
            CashMovement.CreateCashIn(
                CashMovementId.New(), new RegisterSessionId(otherSessionId), new UserId(otherUserId),
                new Money(999m, "MXN"), "De otra sesión", CreatedAtUtc),
            CancellationToken.None);
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var cashInTotal = await repository.GetCashInTotalByRegisterSessionAsync(sessionId, CancellationToken.None);
        var cashOutTotal = await repository.GetCashOutTotalByRegisterSessionAsync(sessionId, CancellationToken.None);

        Assert.Equal(150m, cashInTotal);
        Assert.Equal(30m, cashOutTotal);
    }

    [Fact]
    public async Task SumsReturnZeroWhenTheSessionHasNoMovements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var (registerSessionId, _) = await SeedOpenSessionAndUserAsync(context);
        var repository = new EfCashMovementRepository(context);
        var sessionId = new RegisterSessionId(registerSessionId);

        Assert.Equal(0m, await repository.GetCashInTotalByRegisterSessionAsync(sessionId, CancellationToken.None));
        Assert.Equal(0m, await repository.GetCashOutTotalByRegisterSessionAsync(sessionId, CancellationToken.None));
    }

    [Fact]
    public void ConstructorRejectsNullContext()
    {
        Assert.Throws<ArgumentNullException>(() => new EfCashMovementRepository(null!));
    }
}
