using Pos.Application.AdministrativeNotifications;
using Pos.Application.Authentication;
using Pos.Application.Tests.Common.Time;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Tests.AdministrativeNotifications;

// TAREA 24E, sección 21: seguridad current-user. El servicio nunca acepta OrganizationId/UserId
// desde la UI; siempre los deriva de ICurrentUserSession.
public class AdministrativeNotificationServiceTests
{
    private static readonly DateTimeOffset UtcNow = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(
        AdministrativeNotificationService Service,
        FakeCurrentUserSession UserSession,
        FakeAdministrativeNotificationQuery Query,
        FakeAdministrativeNotificationRepository Repository,
        FakeUnitOfWork UnitOfWork,
        OrganizationId OrganizationId,
        UserId UserId);

    private static Fixture CreateFixture(
        bool authenticated = true,
        IEnumerable<Permission>? permissions = null,
        AdministrativeNotificationPageResult? pageResult = null,
        int unreadCount = 0)
    {
        var organizationId = OrganizationId.New();
        var userId = UserId.New();

        var userSession = new FakeCurrentUserSession();

        if (authenticated)
        {
            userSession.CurrentUser = new AuthenticatedUser(
                userId, organizationId, RoleId.New(), "JPEREZ", "Juan Pérez", "Gerente",
                permissions ?? [Permission.ViewProductAudit]);
        }

        var query = new FakeAdministrativeNotificationQuery(pageResult, unreadCount);
        var repository = new FakeAdministrativeNotificationRepository();
        var unitOfWork = new FakeUnitOfWork();

        var service = new AdministrativeNotificationService(
            userSession, query, repository, unitOfWork, new FakeClock(UtcNow));

        return new Fixture(service, userSession, query, repository, unitOfWork, organizationId, userId);
    }

    // ---------- GetNotificationsAsync ----------

    [Fact]
    public async Task GetNotificationsAsyncReturnsEmptyWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.GetNotificationsAsync(0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.Query.GetForUserCallCount);
    }

    [Fact]
    public async Task GetNotificationsAsyncReturnsEmptyWithoutViewProductAuditPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts]);

        var result = await fixture.Service.GetNotificationsAsync(0, 50);

        Assert.Empty(result.Items);
        Assert.Equal(0, fixture.Query.GetForUserCallCount);
    }

    [Fact]
    public async Task GetNotificationsAsyncDelegatesToQueryScopedToCurrentUserAndOrganization()
    {
        var fixture = CreateFixture();

        await fixture.Service.GetNotificationsAsync(0, 50);

        Assert.Equal(1, fixture.Query.GetForUserCallCount);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
        Assert.Equal(fixture.UserId, fixture.Query.LastUserId);
    }

    // ---------- GetUnreadCountAsync ----------

    [Fact]
    public async Task GetUnreadCountAsyncReturnsZeroWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false, unreadCount: 5);

        var count = await fixture.Service.GetUnreadCountAsync();

        Assert.Equal(0, count);
        Assert.Equal(0, fixture.Query.GetUnreadCountCallCount);
    }

    [Fact]
    public async Task GetUnreadCountAsyncReturnsZeroWithoutViewProductAuditPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts], unreadCount: 5);

        var count = await fixture.Service.GetUnreadCountAsync();

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetUnreadCountAsyncReturnsTheQueryResultScopedToCurrentUser()
    {
        var fixture = CreateFixture(unreadCount: 3);

        var count = await fixture.Service.GetUnreadCountAsync();

        Assert.Equal(3, count);
        Assert.Equal(fixture.OrganizationId, fixture.Query.LastOrganizationId);
        Assert.Equal(fixture.UserId, fixture.Query.LastUserId);
    }

    // ---------- MarkReadAsync ----------

    [Fact]
    public async Task MarkReadAsyncReturnsFalseWhenNotAuthenticated()
    {
        var fixture = CreateFixture(authenticated: false);

        var success = await fixture.Service.MarkReadAsync(AdministrativeNotificationId.New());

        Assert.False(success);
        Assert.Equal(0, fixture.Repository.MarkReadCallCount);
        Assert.Equal(0, fixture.UnitOfWork.CommitCallCount);
    }

    [Fact]
    public async Task MarkReadAsyncReturnsFalseWithoutViewProductAuditPermission()
    {
        var fixture = CreateFixture(permissions: [Permission.ManageProducts]);

        var success = await fixture.Service.MarkReadAsync(AdministrativeNotificationId.New());

        Assert.False(success);
        Assert.Equal(0, fixture.Repository.MarkReadCallCount);
    }

    [Fact]
    public async Task MarkReadAsyncOnlyEverPassesTheCurrentUserIdToTheRepositoryNeverAnArbitraryOne()
    {
        var fixture = CreateFixture();
        var notificationId = AdministrativeNotificationId.New();

        var success = await fixture.Service.MarkReadAsync(notificationId);

        Assert.True(success);
        Assert.Equal(1, fixture.Repository.MarkReadCallCount);
        Assert.Equal(notificationId, fixture.Repository.LastMarkReadNotificationId);
        Assert.Equal(fixture.UserId, fixture.Repository.LastMarkReadUserId);
        Assert.Equal(1, fixture.UnitOfWork.CommitCallCount);
    }
}
