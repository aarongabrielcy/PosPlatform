using Pos.Application.RegisterSessions;
using Pos.Domain.Common.Identifiers;
using Pos.Infrastructure.RegisterSessions;

namespace Pos.Infrastructure.Tests.RegisterSessions;

public class InMemoryCurrentRegisterSessionTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewSessionStartsEmpty()
    {
        var session = new InMemoryCurrentRegisterSession();

        Assert.False(session.IsOpen);
        Assert.Null(session.Current);
    }

    [Fact]
    public void SetActiveSessionMakesTheSessionOpenWithThatValue()
    {
        var session = new InMemoryCurrentRegisterSession();
        var active = CreateActiveRegisterSession("Caja 1");

        session.SetActiveSession(active);

        Assert.True(session.IsOpen);
        Assert.Same(active, session.Current);
    }

    [Fact]
    public void ClearRemovesTheCurrentSessionAndMarksItClosed()
    {
        var session = new InMemoryCurrentRegisterSession();
        session.SetActiveSession(CreateActiveRegisterSession("Caja 1"));

        session.Clear();

        Assert.False(session.IsOpen);
        Assert.Null(session.Current);
    }

    [Fact]
    public void ClearOnAnAlreadyEmptySessionDoesNotThrow()
    {
        var session = new InMemoryCurrentRegisterSession();

        session.Clear();
        session.Clear();

        Assert.False(session.IsOpen);
    }

    [Fact]
    public void SettingANewActiveSessionReplacesThePreviousOneAtomically()
    {
        var session = new InMemoryCurrentRegisterSession();
        var first = CreateActiveRegisterSession("Caja 1");
        var second = CreateActiveRegisterSession("Caja 2");

        session.SetActiveSession(first);
        session.SetActiveSession(second);

        Assert.Same(second, session.Current);
    }

    [Fact]
    public void ActiveRegisterSessionExposedByCurrentIsImmutable()
    {
        var properties = typeof(ActiveRegisterSession).GetProperties();

        Assert.All(properties, property => Assert.False(property.CanWrite));
    }

    [Fact]
    public void SetActiveSessionWithNullThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new InMemoryCurrentRegisterSession().SetActiveSession(null!));

    [Fact]
    public async Task ConcurrentReadsAndWritesDoNotThrowOrCorruptState()
    {
        var session = new InMemoryCurrentRegisterSession();
        var activeSessions = Enumerable.Range(0, 20).Select(i => CreateActiveRegisterSession($"Caja {i}")).ToList();

        var writers = activeSessions.Select(active => Task.Run(() => session.SetActiveSession(active)));
        var readers = Enumerable.Range(0, 20).Select(i => Task.Run(() =>
        {
            _ = session.IsOpen;
            _ = session.Current;
            return i;
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.True(session.IsOpen);
        Assert.Contains(session.Current!.RegisterName, activeSessions.Select(a => a.RegisterName));
    }

    private static ActiveRegisterSession CreateActiveRegisterSession(string registerName) =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            registerName,
            UserId.New(),
            "Cajero Uno",
            OpenedAtUtc,
            100m,
            "MXN");
}
