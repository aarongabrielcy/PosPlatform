using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Authentication;
using Pos.Application.CashMovements;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Register;
using Pos.Desktop.Tests.Main;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Register;

public class RegisterViewModelTests
{
    private static AuthenticatedUser CreateUser(params Permission[] permissions) =>
        new(UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Gerente Uno", "Manager", permissions);

    private static ActiveRegisterSession CreateActiveSession() =>
        new(
            RegisterSessionId.New(), OrganizationId.New(), BranchId.New(), RegisterId.New(), "Caja 1",
            UserId.New(), "Ana Pérez", DateTimeOffset.UtcNow, 100m, "MXN");

    private static RegisterViewModel CreateViewModel(
        FakeCurrentUserSession session, FakeCurrentRegisterSession? registerSession = null, FakeCashMovementService? service = null) =>
        new(registerSession ?? new FakeCurrentRegisterSession(), session, service ?? new FakeCashMovementService(),
            NullLogger<RegisterViewModel>.Instance);

    // ---------- Autorización (sección 47): Manager/Admin ven las acciones, Cashier no ----------

    [Fact]
    public void ManagerCanManageCashMovements()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ManageCashMovements) };
        var viewModel = CreateViewModel(session);

        Assert.True(viewModel.CanManageCashMovements);
    }

    [Fact]
    public void CashierCannotManageCashMovements()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ProcessSale) };
        var viewModel = CreateViewModel(session);

        Assert.False(viewModel.CanManageCashMovements);
    }

    [Fact]
    public void UnauthenticatedUserCannotManageCashMovements()
    {
        var session = new FakeCurrentUserSession();
        var viewModel = CreateViewModel(session);

        Assert.False(viewModel.CanManageCashMovements);
    }

    [Fact]
    public void ManagerCanViewCashMovements()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ViewCashTotals) };
        var viewModel = CreateViewModel(session);

        Assert.True(viewModel.CanViewCashMovements);
    }

    [Fact]
    public void CashierCannotViewCashMovements()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ProcessSale) };
        var viewModel = CreateViewModel(session);

        Assert.False(viewModel.CanViewCashMovements);
    }

    // ---------- CashInCommand/CashOutCommand: solo piden, nunca abren ventanas ----------

    [Fact]
    public void CashInCommandRaisesCashInRequestedWhenAuthorized()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ManageCashMovements) };
        var viewModel = CreateViewModel(session);

        var raised = false;
        viewModel.CashInRequested += (_, _) => raised = true;

        viewModel.CashInCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void CashInCommandCannotExecuteWhenNotAuthorized()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ProcessSale) };
        var viewModel = CreateViewModel(session);

        Assert.False(viewModel.CashInCommand.CanExecute(null));
        Assert.False(viewModel.CashOutCommand.CanExecute(null));
    }

    // ---------- LoadMovementsAsync (sección 21) ----------

    [Fact]
    public async Task LoadMovementsAsyncPopulatesMovementsWhenAuthorizedAndSessionIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ViewCashTotals) };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveSession() };
        var entry = new CashMovementEntry(
            Guid.NewGuid(), CashMovementType.CashIn, 100m, "MXN", "Reposición", Guid.NewGuid(), "Ana Pérez", DateTimeOffset.UtcNow);
        var service = new FakeCashMovementService
        {
            GetCurrentSessionMovementsHandler = () => Task.FromResult(CashMovementListResult.SuccessResult([entry])),
        };
        var viewModel = CreateViewModel(session, registerSession, service);

        await viewModel.LoadMovementsAsync();

        var row = Assert.Single(viewModel.Movements);
        Assert.Equal("Entrada", row.TypeText);
        Assert.Equal("Reposición", row.Reason);
    }

    [Fact]
    public async Task LoadMovementsAsyncClearsMovementsWhenNotAuthorized()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ProcessSale) };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveSession() };
        var service = new FakeCashMovementService();
        var viewModel = CreateViewModel(session, registerSession, service);

        await viewModel.LoadMovementsAsync();

        Assert.Empty(viewModel.Movements);
        Assert.Equal(0, service.GetCurrentSessionMovementsCallCount);
    }

    [Fact]
    public async Task LoadMovementsAsyncClearsMovementsWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ViewCashTotals) };
        var registerSession = new FakeCurrentRegisterSession();
        var service = new FakeCashMovementService();
        var viewModel = CreateViewModel(session, registerSession, service);

        await viewModel.LoadMovementsAsync();

        Assert.Empty(viewModel.Movements);
        Assert.Equal(0, service.GetCurrentSessionMovementsCallCount);
    }

    [Fact]
    public async Task LoadMovementsAsyncSetsAnErrorWhenTheServiceFails()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ViewCashTotals) };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveSession() };
        var service = new FakeCashMovementService
        {
            GetCurrentSessionMovementsHandler = () =>
                Task.FromResult(CashMovementListResult.Failure(CashMovementResultStatus.SessionNotFound)),
        };
        var viewModel = CreateViewModel(session, registerSession, service);

        await viewModel.LoadMovementsAsync();

        Assert.NotNull(viewModel.MovementsError);
    }
}
