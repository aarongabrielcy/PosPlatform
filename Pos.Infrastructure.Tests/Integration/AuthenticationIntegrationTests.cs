using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pos.Application.Authentication;
using Pos.Application.Bootstrap;
using Pos.Application.Common.Time;
using Pos.Infrastructure.Authentication;
using Pos.Infrastructure.Persistence;
using Pos.Infrastructure.Persistence.Records;
using Pos.Infrastructure.Persistence.Repositories;
using Pos.Infrastructure.Security;

namespace Pos.Infrastructure.Tests.Integration;

public class AuthenticationIntegrationTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    // Reduce iteraciones de PBKDF2 solo para que la prueba de integración no dependa de la
    // latencia real de producción (210,000 iteraciones); el algoritmo real sigue siendo PBKDF2-HMAC-SHA256.
    private const int TestHasherIterations = 1000;
    private const string AdminPassword = "SuperSecret123";

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

    private static async Task<(PosDbContext Context, SqliteConnection Connection, Pbkdf2PasswordHasher Hasher)> CreateBootstrappedDatabaseAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();

        var hasher = new Pbkdf2PasswordHasher(TestHasherIterations);
        var bootstrapService = new InitialBusinessBootstrapService(
            new EfOrganizationRepository(context),
            new EfBranchRepository(context),
            new EfRegisterRepository(context),
            new EfRoleRepository(context),
            new EfUserRepository(context),
            context,
            new FixedClock(FixedNow),
            hasher);

        var bootstrapRequest = new InitialBusinessBootstrapRequest(
            "Acme Retail", "Main Branch", "Register 1", "admin", "Administrator", AdminPassword);
        var bootstrapResult = await bootstrapService.BootstrapAsync(bootstrapRequest, CancellationToken.None);

        Assert.Equal(InitialBusinessBootstrapStatus.Created, bootstrapResult.Status);

        context.ChangeTracker.Clear();

        return (context, connection, hasher);
    }

    private static AuthenticationService CreateAuthenticationService(
        PosDbContext context, Pbkdf2PasswordHasher hasher, ICurrentUserSessionWriter sessionWriter) =>
        new(
            new EfOrganizationRepository(context),
            new EfUserRepository(context),
            new EfRoleRepository(context),
            hasher,
            sessionWriter);

    private static async Task<int> TotalRowCountAsync(PosDbContext context) =>
        await context.Set<OrganizationRecord>().CountAsync()
        + await context.Set<BranchRecord>().CountAsync()
        + await context.Set<RegisterRecord>().CountAsync()
        + await context.Set<RoleRecord>().CountAsync()
        + await context.Set<UserRecord>().CountAsync();

    [Fact]
    public async Task ValidAdministratorCredentialsAuthenticateSuccessfullyWithRoleAndPermissionsLoaded()
    {
        var (context, connection, hasher) = await CreateBootstrappedDatabaseAsync();
        await using var _ = connection;
        await using var __ = context;

        var session = new InMemoryCurrentUserSession();
        var service = CreateAuthenticationService(context, hasher, session);

        var result = await service.AuthenticateAsync(
            new AuthenticationRequest("admin", AdminPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.Success, result.Status);
        Assert.NotNull(result.AuthenticatedUser);
        Assert.Equal("ADMIN", result.AuthenticatedUser!.Username);
        Assert.Equal("Administrator", result.AuthenticatedUser.RoleName);
        Assert.Equal(Enum.GetValues<Pos.Domain.Security.Permission>().Length, result.AuthenticatedUser.Permissions.Count);

        Assert.True(session.IsAuthenticated);
        Assert.Same(result.AuthenticatedUser, session.CurrentUser);
    }

    [Fact]
    public async Task WrongPasswordFailsAuthenticationWithoutEstablishingSession()
    {
        var (context, connection, hasher) = await CreateBootstrappedDatabaseAsync();
        await using var _ = connection;
        await using var __ = context;

        var session = new InMemoryCurrentUserSession();
        var service = CreateAuthenticationService(context, hasher, session);

        var result = await service.AuthenticateAsync(
            new AuthenticationRequest("admin", "WrongPassword1"), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InvalidCredentials, result.Status);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task InactiveUserFailsAuthenticationWithoutEstablishingSession()
    {
        var (context, connection, hasher) = await CreateBootstrappedDatabaseAsync();
        await using var _ = connection;
        await using var __ = context;

        var userRecord = await context.Set<UserRecord>().SingleAsync();
        userRecord.IsActive = false;
        await context.CommitAsync(CancellationToken.None);
        context.ChangeTracker.Clear();

        var session = new InMemoryCurrentUserSession();
        var service = CreateAuthenticationService(context, hasher, session);

        var result = await service.AuthenticateAsync(
            new AuthenticationRequest("admin", AdminPassword), CancellationToken.None);

        Assert.Equal(AuthenticationStatus.InactiveUser, result.Status);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public async Task LoginAndLogoutDoNotChangeAnyDataInTheDatabase()
    {
        var (context, connection, hasher) = await CreateBootstrappedDatabaseAsync();
        await using var _ = connection;
        await using var __ = context;

        var rowCountBefore = await TotalRowCountAsync(context);
        var userBefore = await context.Set<UserRecord>().AsNoTracking().SingleAsync();

        var session = new InMemoryCurrentUserSession();
        var service = CreateAuthenticationService(context, hasher, session);

        var result = await service.AuthenticateAsync(
            new AuthenticationRequest("admin", AdminPassword), CancellationToken.None);
        Assert.Equal(AuthenticationStatus.Success, result.Status);

        session.Clear();

        var rowCountAfter = await TotalRowCountAsync(context);
        var userAfter = await context.Set<UserRecord>().AsNoTracking().SingleAsync();

        Assert.Equal(rowCountBefore, rowCountAfter);
        Assert.Equal(userBefore.PasswordHash, userAfter.PasswordHash);
        Assert.Equal(userBefore.IsActive, userAfter.IsActive);
        Assert.False(session.IsAuthenticated);
    }
}
