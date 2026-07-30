using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Bootstrap;
using Pos.Desktop.Setup;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Setup;

public class InitialSetupViewModelTests
{
    private const string ValidPassword = "SuperSecret123";

    [Fact]
    public void EmptyOrganizationNameShowsValidationErrorWithoutCallingBootstrap()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.OrganizationName = "   ";
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal("El nombre del negocio es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, bootstrapService.CallCount);
    }

    [Fact]
    public void ShortPasswordShowsValidationErrorWithoutCallingBootstrap()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials("Sh0rt12", "Sh0rt12");

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal("La contraseña debe tener entre 8 y 256 caracteres.", viewModel.GeneralError);
        Assert.Equal(0, bootstrapService.CallCount);
    }

    [Fact]
    public void MismatchedPasswordsShowValidationErrorWithoutCallingBootstrap()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, "DifferentPass1");

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal("Las contraseñas no coinciden.", viewModel.GeneralError);
        Assert.Equal(0, bootstrapService.CallCount);
    }

    [Fact]
    public void ValidSubmissionSendsTrimmedRequestToBootstrapService()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        viewModel.OrganizationName = "  Acme Retail  ";
        viewModel.BranchName = "  Main Branch  ";
        viewModel.RegisterName = "  Register 1  ";
        viewModel.AdministratorUsername = "  admin  ";
        viewModel.AdministratorDisplayName = "  Administrator  ";
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(1, bootstrapService.CallCount);
        var request = bootstrapService.LastRequest!;
        Assert.Equal("Acme Retail", request.OrganizationName);
        Assert.Equal("Main Branch", request.BranchName);
        Assert.Equal("Register 1", request.RegisterName);
        Assert.Equal("admin", request.AdministratorUsername);
        Assert.Equal("Administrator", request.AdministratorDisplayName);
        Assert.Equal(ValidPassword, request.AdministratorPassword);
    }

    [Fact]
    public void CreatedResultRaisesSetupCompleted()
    {
        var result = InitialBusinessBootstrapResult.Created(
            OrganizationId.New(), BranchId.New(), RegisterId.New(), RoleId.New(), UserId.New());
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) => Task.FromResult(result));
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var raised = false;
        viewModel.SetupCompleted += (_, _) => raised = true;

        viewModel.SubmitCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(viewModel.GeneralError);
    }

    [Fact]
    public void AlreadyInitializedResultRaisesSetupCompleted()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var raised = false;
        viewModel.SetupCompleted += (_, _) => raised = true;

        viewModel.SubmitCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void InconsistentStateShowsGenericErrorAndDoesNotRaiseSetupCompleted()
    {
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) =>
            throw new InitialBusinessBootstrapStateException("Detalle técnico del estado inconsistente."));
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var raised = false;
        viewModel.SetupCompleted += (_, _) => raised = true;

        viewModel.SubmitCommand.Execute(null);

        Assert.False(raised);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.GeneralError);
        Assert.DoesNotContain("Detalle técnico", viewModel.GeneralError, StringComparison.Ordinal);
    }

    [Fact]
    public void UnexpectedExceptionShowsGenericErrorAndDoesNotRaiseSetupCompleted()
    {
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) =>
            throw new InvalidOperationException("Detalle técnico interno."));
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var raised = false;
        viewModel.SetupCompleted += (_, _) => raised = true;

        viewModel.SubmitCommand.Execute(null);

        Assert.False(raised);
        Assert.False(viewModel.IsBusy);
        Assert.NotNull(viewModel.GeneralError);
        Assert.DoesNotContain("Detalle técnico interno.", viewModel.GeneralError, StringComparison.Ordinal);
    }

    [Fact]
    public void CancellationDuringBootstrapIsHandledSilentlyWithoutError()
    {
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) =>
            throw new OperationCanceledException());
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var raised = false;
        viewModel.SetupCompleted += (_, _) => raised = true;

        viewModel.SubmitCommand.Execute(null);

        Assert.False(raised);
        Assert.Null(viewModel.GeneralError);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task IsBusyIsTrueWhileBootstrapIsRunningAndFalseAfterCompletion()
    {
        var workSource = new TaskCompletionSource<InitialBusinessBootstrapResult>();
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(InitialSetupViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.SubmitCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        workSource.SetResult(InitialBusinessBootstrapResult.AlreadyInitialized());
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void SecondSubmitWhileRunningIsIgnored()
    {
        var workSource = new TaskCompletionSource<InitialBusinessBootstrapResult>();
        var bootstrapService = new FakeInitialBusinessBootstrapService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        viewModel.SubmitCommand.Execute(null);
        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(1, bootstrapService.CallCount);

        workSource.SetResult(InitialBusinessBootstrapResult.AlreadyInitialized());
    }

    [Fact]
    public void PasswordIsNotRetainedAfterSuccessfulExecution()
    {
        var bootstrapService = SucceedingBootstrapService();
        var viewModel = CreateViewModel(bootstrapService);
        FillValidFields(viewModel);
        viewModel.SetPendingCredentials(ValidPassword, ValidPassword);

        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(1, bootstrapService.CallCount);

        // Sin volver a llamar SetPendingCredentials: si la contraseña se hubiera retenido, esta
        // segunda ejecución reutilizaría ValidPassword en lugar de fallar la validación.
        viewModel.SubmitCommand.Execute(null);

        Assert.Equal(1, bootstrapService.CallCount);
        Assert.Equal("La contraseña es obligatoria.", viewModel.GeneralError);
    }

    private static InitialSetupViewModel CreateViewModel(FakeInitialBusinessBootstrapService bootstrapService) =>
        new(bootstrapService, NullLogger<InitialSetupViewModel>.Instance);

    private static FakeInitialBusinessBootstrapService SucceedingBootstrapService() =>
        new((_, _) => Task.FromResult(InitialBusinessBootstrapResult.AlreadyInitialized()));

    private static void FillValidFields(InitialSetupViewModel viewModel)
    {
        viewModel.OrganizationName = "Acme Retail";
        viewModel.BranchName = "Main Branch";
        viewModel.RegisterName = "Register 1";
        viewModel.AdministratorUsername = "admin";
        viewModel.AdministratorDisplayName = "Administrator";
    }
}
