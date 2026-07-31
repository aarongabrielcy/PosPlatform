using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Main;

public class MainWindowViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExposesDisplayNameAndRoleNameFromTheCurrentSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        Assert.Equal("Ana Pérez", viewModel.DisplayName);
        Assert.Equal("Cajero", viewModel.RoleName);
    }

    [Fact]
    public void MissingSessionProducesEmptyDisplayNameAndRoleNameInsteadOfThrowing()
    {
        var session = new FakeCurrentUserSession();
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(string.Empty, viewModel.RoleName);
    }

    [Fact]
    public void ExposesRegisterDataFromTheCurrentRegisterSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = new MainWindowViewModel(session, registerSession);

        Assert.True(viewModel.IsRegisterOpen);
        Assert.Equal("Caja 1", viewModel.RegisterName);
        Assert.Equal("Caja abierta", viewModel.RegisterStatusText);
        Assert.Contains("100", viewModel.RegisterOpeningAmountText);
        Assert.Contains("MXN", viewModel.RegisterOpeningAmountText);
    }

    [Fact]
    public void NoOpenRegisterSessionProducesEmptyRegisterDataInsteadOfThrowing()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        Assert.False(viewModel.IsRegisterOpen);
        Assert.Equal(string.Empty, viewModel.RegisterName);
        Assert.Equal(string.Empty, viewModel.RegisterStatusText);
        Assert.Equal(string.Empty, viewModel.RegisterOpeningAmountText);
    }

    [Fact]
    public void LogoutCommandClearsTheSessionWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(1, session.ClearCallCount);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void LogoutCommandRaisesLogoutRequestedWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void LogoutCommandIsBlockedWhenARegisterSessionIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = new MainWindowViewModel(session, registerSession);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal(0, session.ClearCallCount);
        Assert.True(session.IsAuthenticated);
        Assert.False(string.IsNullOrEmpty(viewModel.LogoutBlockedMessage));
    }

    [Fact]
    public void LoggingOutTwiceDoesNotThrow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session, new FakeCurrentRegisterSession());

        viewModel.LogoutCommand.Execute(null);
        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(2, session.ClearCallCount);
    }

    [Fact]
    public void CloseRegisterCommandRaisesCloseRegisterRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = new MainWindowViewModel(session, registerSession);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, registerSession.ClearCallCount);
    }

    [Fact]
    public void ConstructorDoesNotAcceptAServiceProviderOrResolveWindows()
    {
        var constructorParameterTypes = typeof(MainWindowViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        Assert.DoesNotContain("IServiceProvider", constructorParameterTypes);
    }

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);

    private static ActiveRegisterSession CreateActiveRegisterSession() =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            "Caja 1",
            UserId.New(),
            "Ana Pérez",
            OpenedAtUtc,
            100m,
            "MXN");
}
