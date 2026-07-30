using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Authentication;
using Pos.Desktop.Login;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Login;

public class LoginViewModelTests
{
    private const string ValidPassword = "SuperSecret123";

    [Fact]
    public void EmptyUsernameShowsValidationErrorWithoutCallingAuthenticationService()
    {
        var authService = SucceedingAuthenticationService();
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "   ";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);

        Assert.Equal("El usuario es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, authService.CallCount);
    }

    [Fact]
    public void EmptyPasswordShowsValidationErrorWithoutCallingAuthenticationService()
    {
        var authService = SucceedingAuthenticationService();
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";

        viewModel.LoginCommand.Execute(null);

        Assert.Equal("La contraseña es obligatoria.", viewModel.GeneralError);
        Assert.Equal(0, authService.CallCount);
    }

    [Fact]
    public void PasswordIsNotRetainedAcrossExecutions()
    {
        var authService = SucceedingAuthenticationService();
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);
        Assert.Equal(1, authService.CallCount);

        // Sin volver a llamar SetPendingPassword: si la contraseña se hubiera retenido, esta
        // segunda ejecución reutilizaría ValidPassword en lugar de fallar la validación.
        viewModel.LoginCommand.Execute(null);

        Assert.Equal(1, authService.CallCount);
        Assert.Equal("La contraseña es obligatoria.", viewModel.GeneralError);
    }

    [Fact]
    public void DoubleSubmitWhileRunningIsIgnored()
    {
        var workSource = new TaskCompletionSource<AuthenticationResult>();
        var authService = new FakeAuthenticationService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);
        viewModel.LoginCommand.Execute(null);

        Assert.Equal(1, authService.CallCount);

        workSource.SetResult(SuccessResult());
    }

    [Fact]
    public async Task IsBusyIsTrueWhileAuthenticatingAndFalseAfterCompletion()
    {
        var workSource = new TaskCompletionSource<AuthenticationResult>();
        var authService = new FakeAuthenticationService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LoginViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.LoginCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        workSource.SetResult(SuccessResult());
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void SuccessResultRaisesLoginSucceeded()
    {
        var authService = SucceedingAuthenticationService();
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        viewModel.LoginCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(viewModel.GeneralError);
    }

    [Fact]
    public void InvalidCredentialsShowsGenericMessageWithoutEnumeratingUsers()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials)));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "ghost";
        viewModel.SetPendingPassword(ValidPassword);

        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        viewModel.LoginCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("Usuario o contraseña incorrectos.", viewModel.GeneralError);
    }

    [Fact]
    public void InactiveUserShowsGenericAccountUnavailableMessage()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InactiveUser)));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);

        Assert.Equal("La cuenta no está disponible. Contacta al administrador.", viewModel.GeneralError);
    }

    [Fact]
    public void InactiveRoleShowsGenericAccountUnavailableMessage()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InactiveRole)));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);

        Assert.Equal("La cuenta no está disponible. Contacta al administrador.", viewModel.GeneralError);
    }

    [Fact]
    public void InvalidInstallationStateShowsGenericInstallationMessage()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InvalidInstallationState)));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);

        Assert.Equal("La instalación presenta una configuración inválida.", viewModel.GeneralError);
    }

    [Fact]
    public void UnexpectedExceptionShowsGenericErrorAndDoesNotRaiseLoginSucceeded()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => throw new InvalidOperationException("Detalle técnico interno."));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        viewModel.LoginCommand.Execute(null);

        Assert.False(raised);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.GeneralError);
        Assert.DoesNotContain("Detalle técnico interno.", viewModel.GeneralError, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationWhileAuthenticatingIsHandledSilentlyWithoutError()
    {
        var authService = new FakeAuthenticationService((_, _) => throw new OperationCanceledException());
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        var raised = false;
        viewModel.LoginSucceeded += (_, _) => raised = true;

        viewModel.LoginCommand.Execute(null);

        Assert.False(raised);
        Assert.Null(viewModel.GeneralError);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void PasswordNeverAppearsInGeneralErrorMessages()
    {
        var authService = new FakeAuthenticationService(
            (_, _) => Task.FromResult(AuthenticationResult.Failure(AuthenticationStatus.InvalidCredentials)));
        var viewModel = CreateViewModel(authService);
        viewModel.Username = "admin";
        viewModel.SetPendingPassword(ValidPassword);

        viewModel.LoginCommand.Execute(null);

        Assert.DoesNotContain(ValidPassword, viewModel.GeneralError, StringComparison.Ordinal);
    }

    private static LoginViewModel CreateViewModel(FakeAuthenticationService authService) =>
        new(authService, NullLogger<LoginViewModel>.Instance);

    private static FakeAuthenticationService SucceedingAuthenticationService() =>
        new((_, _) => Task.FromResult(SuccessResult()));

    private static AuthenticationResult SuccessResult() =>
        AuthenticationResult.Success(new AuthenticatedUser(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "ADMIN",
            "Administrator",
            "Administrator",
            [Permission.ProcessSale]));
}
