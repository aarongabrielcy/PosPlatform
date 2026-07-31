using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Desktop.RegisterSessions;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.RegisterSessions;

public class OpenRegisterSessionViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmptyAmountShowsValidationErrorWithoutCallingTheService()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "   ";

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("El fondo inicial es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.OpenCallCount);
    }

    [Fact]
    public async Task InvalidDecimalShowsValidationErrorWithoutCallingTheService()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "no-es-un-numero";

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("El fondo inicial no es un monto válido.", viewModel.GeneralError);
        Assert.Equal(0, service.OpenCallCount);
    }

    [Fact]
    public async Task NegativeAmountShowsValidationErrorWithoutCallingTheService()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "-1";

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("El fondo inicial no puede ser negativo.", viewModel.GeneralError);
        Assert.Equal(0, service.OpenCallCount);
    }

    [Fact]
    public async Task ZeroAmountIsValidAndOpensSuccessfully()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) => Task.FromResult(RegisterSessionResult.OpenSuccess(CreateActiveRegisterSession())));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "0";

        var raised = false;
        viewModel.RegisterOpened += (_, _) => raised = true;

        viewModel.OpenCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(1, service.OpenCallCount);
        Assert.Equal(0m, service.LastOpenRequest!.OpeningAmount);
    }

    [Fact]
    public async Task SuccessfulOpenRaisesRegisterOpenedWithTheActiveSession()
    {
        var activeSession = CreateActiveRegisterSession();
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) => Task.FromResult(RegisterSessionResult.OpenSuccess(activeSession)));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        ActiveRegisterSession? received = null;
        viewModel.RegisterOpened += (_, session) => received = session;

        viewModel.OpenCommand.Execute(null);

        Assert.Same(activeSession, received);
        Assert.Null(viewModel.GeneralError);
    }

    [Fact]
    public async Task AlreadyOpenErrorFromTheServiceShowsAMessageWithoutRaisingRegisterOpened()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) => Task.FromResult(RegisterSessionResult.Failure(RegisterSessionResultStatus.AlreadyOpen)));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        var raised = false;
        viewModel.RegisterOpened += (_, _) => raised = true;

        viewModel.OpenCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("Ya existe una sesión abierta para esta caja.", viewModel.GeneralError);
    }

    [Fact]
    public async Task InvalidInstallationStateFromTheServiceShowsAMessage()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) =>
                Task.FromResult(RegisterSessionResult.Failure(RegisterSessionResultStatus.InvalidInstallationState)));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("La instalación presenta una configuración inválida.", viewModel.GeneralError);
    }

    [Fact]
    public async Task NoActiveRegistersShowsAMessageAndBlocksSubmission()
    {
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult<IReadOnlyList<AvailableRegister>>(Array.Empty<AvailableRegister>()));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("No hay ninguna caja activa disponible. Contacte al administrador.", viewModel.GeneralError);
        Assert.Equal(0, service.OpenCallCount);
    }

    [Fact]
    public async Task MultipleActiveRegistersRequireExplicitSelection()
    {
        var registers = new List<AvailableRegister>
        {
            new(RegisterId.New(), "Caja 1"),
            new(RegisterId.New(), "Caja 2"),
        };
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult<IReadOnlyList<AvailableRegister>>(registers));
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        Assert.True(viewModel.RequiresRegisterSelection);
        Assert.Null(viewModel.SelectedRegister);

        viewModel.OpenCommand.Execute(null);

        Assert.Equal("Seleccione una caja.", viewModel.GeneralError);
        Assert.Equal(0, service.OpenCallCount);
    }

    [Fact]
    public async Task SingleActiveRegisterIsAutoSelected()
    {
        var register = new AvailableRegister(RegisterId.New(), "Caja 1");
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult<IReadOnlyList<AvailableRegister>>([register]));
        var viewModel = await CreateInitializedViewModelAsync(service);

        Assert.False(viewModel.RequiresRegisterSelection);
        Assert.Equal(register, viewModel.SelectedRegister);
    }

    [Fact]
    public async Task IsBusyIsTrueWhileOpeningAndFalseAfterCompletion()
    {
        var workSource = new TaskCompletionSource<RegisterSessionResult>();
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) => workSource.Task);
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(OpenRegisterSessionViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.OpenCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        workSource.SetResult(RegisterSessionResult.OpenSuccess(CreateActiveRegisterSession()));
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task DoubleSubmitWhileRunningIsIgnored()
    {
        var workSource = new TaskCompletionSource<RegisterSessionResult>();
        var service = new FakeRegisterSessionService(
            getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()),
            openHandler: (_, _) => workSource.Task);
        var viewModel = await CreateInitializedViewModelAsync(service);
        viewModel.OpeningAmountText = "100";

        viewModel.OpenCommand.Execute(null);
        viewModel.OpenCommand.Execute(null);

        Assert.Equal(1, service.OpenCallCount);

        workSource.SetResult(RegisterSessionResult.OpenSuccess(CreateActiveRegisterSession()));
    }

    [Fact]
    public async Task LogoutCommandClearsTheUserSessionAndRaisesLogoutRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser() };
        var service = new FakeRegisterSessionService(getAvailableRegistersHandler: _ => Task.FromResult(SingleRegister()));
        var viewModel = new OpenRegisterSessionViewModel(service, session, NullLogger<OpenRegisterSessionViewModel>.Instance);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(1, session.ClearCallCount);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void ConstructorDoesNotAcceptRepositoriesOrAServiceProvider()
    {
        var constructorParameterTypes = typeof(OpenRegisterSessionViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToList();

        Assert.DoesNotContain("IServiceProvider", constructorParameterTypes);
        Assert.DoesNotContain(constructorParameterTypes, name => name.Contains("Repository", StringComparison.Ordinal));
        Assert.DoesNotContain(constructorParameterTypes, name => name.Contains("DbContext", StringComparison.Ordinal));
    }

    private static async Task<OpenRegisterSessionViewModel> CreateInitializedViewModelAsync(
        FakeRegisterSessionService service)
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser() };
        var viewModel = new OpenRegisterSessionViewModel(service, session, NullLogger<OpenRegisterSessionViewModel>.Instance);

        await viewModel.InitializeAsync();

        return viewModel;
    }

    private static IReadOnlyList<AvailableRegister> SingleRegister() =>
        [new AvailableRegister(RegisterId.New(), "Caja 1")];

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

    private static AuthenticatedUser CreateAuthenticatedUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            "Ana Pérez",
            "Cajero",
            [Permission.OpenRegisterSession]);
}
