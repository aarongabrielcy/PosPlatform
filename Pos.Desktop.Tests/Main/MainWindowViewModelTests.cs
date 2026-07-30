using Pos.Application.Authentication;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Main;

public class MainWindowViewModelTests
{
    [Fact]
    public void ExposesDisplayNameAndRoleNameFromTheCurrentSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session);

        Assert.Equal("Ana Pérez", viewModel.DisplayName);
        Assert.Equal("Cajero", viewModel.RoleName);
    }

    [Fact]
    public void MissingSessionProducesEmptyDisplayNameAndRoleNameInsteadOfThrowing()
    {
        var session = new FakeCurrentUserSession();
        var viewModel = new MainWindowViewModel(session);

        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(string.Empty, viewModel.RoleName);
    }

    [Fact]
    public void LogoutCommandClearsTheSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session);

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(1, session.ClearCallCount);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void LogoutCommandRaisesLogoutRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void LoggingOutTwiceDoesNotThrow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = new MainWindowViewModel(session);

        viewModel.LogoutCommand.Execute(null);
        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(2, session.ClearCallCount);
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
}
