using Pos.Application.Authentication;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;
using Pos.Infrastructure.Authentication;

namespace Pos.Infrastructure.Tests.Authentication;

public class InMemoryCurrentUserSessionTests
{
    [Fact]
    public void NewSessionStartsUnauthenticatedWithNoCurrentUser()
    {
        var session = new InMemoryCurrentUserSession();

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.CurrentUser);
    }

    [Fact]
    public void SetAuthenticatedUserMakesTheSessionAuthenticatedWithThatUser()
    {
        var session = new InMemoryCurrentUserSession();
        var user = CreateAuthenticatedUser("ADMIN");

        session.SetAuthenticatedUser(user);

        Assert.True(session.IsAuthenticated);
        Assert.Same(user, session.CurrentUser);
    }

    [Fact]
    public void ClearRemovesTheCurrentUserAndMarksTheSessionUnauthenticated()
    {
        var session = new InMemoryCurrentUserSession();
        session.SetAuthenticatedUser(CreateAuthenticatedUser("ADMIN"));

        session.Clear();

        Assert.False(session.IsAuthenticated);
        Assert.Null(session.CurrentUser);
    }

    [Fact]
    public void ClearOnAnAlreadyEmptySessionDoesNotThrow()
    {
        var session = new InMemoryCurrentUserSession();

        session.Clear();
        session.Clear();

        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void SettingANewUserReplacesThePreviousOneAtomically()
    {
        var session = new InMemoryCurrentUserSession();
        var firstUser = CreateAuthenticatedUser("FIRST");
        var secondUser = CreateAuthenticatedUser("SECOND");

        session.SetAuthenticatedUser(firstUser);
        session.SetAuthenticatedUser(secondUser);

        Assert.Same(secondUser, session.CurrentUser);
    }

    [Fact]
    public void SetAuthenticatedUserRaisesSessionChanged()
    {
        var session = new InMemoryCurrentUserSession();
        var raised = 0;
        session.SessionChanged += (_, _) => raised++;

        session.SetAuthenticatedUser(CreateAuthenticatedUser("ADMIN"));

        Assert.Equal(1, raised);
    }

    [Fact]
    public void ClearRaisesSessionChangedOnlyWhenThereWasAUserToClear()
    {
        var session = new InMemoryCurrentUserSession();
        var raised = 0;
        session.SessionChanged += (_, _) => raised++;

        session.Clear();
        Assert.Equal(0, raised);

        session.SetAuthenticatedUser(CreateAuthenticatedUser("ADMIN"));
        session.Clear();
        Assert.Equal(2, raised);
    }

    [Fact]
    public void SetAuthenticatedUserWithNullThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new InMemoryCurrentUserSession().SetAuthenticatedUser(null!));

    [Fact]
    public async Task ConcurrentReadsAndWritesDoNotThrowOrCorruptState()
    {
        var session = new InMemoryCurrentUserSession();
        var users = Enumerable.Range(0, 20).Select(i => CreateAuthenticatedUser($"USER{i}")).ToList();

        var writers = users.Select(user => Task.Run(() => session.SetAuthenticatedUser(user)));
        var readers = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            _ = session.IsAuthenticated;
            _ = session.CurrentUser;
            return i;
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.True(session.IsAuthenticated);
        Assert.Contains(session.CurrentUser!.Username, users.Select(u => u.Username));
    }

    private static AuthenticatedUser CreateAuthenticatedUser(string username) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            username,
            "Display " + username,
            "Administrator",
            [Permission.ProcessSale]);
}
