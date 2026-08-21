using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.CashMovements;
using Pos.Desktop.CashMovements;
using Pos.Desktop.Tests.Register;
using Pos.Domain.CashMovements;

namespace Pos.Desktop.Tests.CashMovements;

public class RecordCashMovementViewModelTests
{
    private static RecordCashMovementViewModel CreateViewModel(FakeCashMovementService service) =>
        new(service, NullLogger<RecordCashMovementViewModel>.Instance);

    [Fact]
    public void LoadCashInSetsTitleAndConfirmButtonText()
    {
        var viewModel = CreateViewModel(new FakeCashMovementService());

        viewModel.Load(CashMovementType.CashIn);

        Assert.Equal("Entrada de efectivo", viewModel.Title);
        Assert.Equal("Registrar entrada", viewModel.ConfirmButtonText);
    }

    [Fact]
    public void LoadCashOutSetsTitleAndConfirmButtonText()
    {
        var viewModel = CreateViewModel(new FakeCashMovementService());

        viewModel.Load(CashMovementType.CashOut);

        Assert.Equal("Salida de efectivo", viewModel.Title);
        Assert.Equal("Registrar salida", viewModel.ConfirmButtonText);
    }

    [Fact]
    public async Task ConfirmCommandRejectsAnEmptyOrNonNumericAmount()
    {
        var service = new FakeCashMovementService();
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = "abc";
        viewModel.ReasonText = "Motivo válido";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-10")]
    public async Task ConfirmCommandRejectsZeroOrNegativeAmount(string amountText)
    {
        var service = new FakeCashMovementService();
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = amountText;
        viewModel.ReasonText = "Motivo válido";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
    }

    [Fact]
    public async Task ConfirmCommandRejectsABlankReasonWithoutCallingTheService()
    {
        var service = new FakeCashMovementService();
        var calls = 0;
        service.RecordCashInHandler = _ =>
        {
            calls++;
            return Task.FromResult(CashMovementResult.SuccessResult(CreateEntry()));
        };
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = "10";
        viewModel.ReasonText = "   ";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task ConfirmCommandCallsRecordCashInForCashInType()
    {
        var service = new FakeCashMovementService();
        RecordCashMovementRequest? captured = null;
        service.RecordCashInHandler = request =>
        {
            captured = request;
            return Task.FromResult(CashMovementResult.SuccessResult(CreateEntry()));
        };
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = "150.5";
        viewModel.ReasonText = "Reposición de efectivo";

        CashMovementEntry? confirmed = null;
        viewModel.Confirmed += (_, entry) => confirmed = entry;

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(captured);
        Assert.Equal(150.5m, captured!.Amount);
        Assert.Equal("Reposición de efectivo", captured.Reason);
        Assert.NotNull(confirmed);
    }

    [Fact]
    public async Task ConfirmCommandCallsRecordCashOutForCashOutType()
    {
        var service = new FakeCashMovementService();
        var cashOutCalls = 0;
        var cashInCalls = 0;
        service.RecordCashOutHandler = _ =>
        {
            cashOutCalls++;
            return Task.FromResult(CashMovementResult.SuccessResult(CreateEntry(CashMovementType.CashOut)));
        };
        service.RecordCashInHandler = _ =>
        {
            cashInCalls++;
            return Task.FromResult(CashMovementResult.SuccessResult(CreateEntry()));
        };
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashOut);
        viewModel.AmountText = "40";
        viewModel.ReasonText = "Pago de mensajería";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, cashOutCalls);
        Assert.Equal(0, cashInCalls);
    }

    [Fact]
    public async Task FailedRecordShowsAMessageWithoutRaisingConfirmed()
    {
        var service = new FakeCashMovementService
        {
            RecordCashInHandler = _ => Task.FromResult(CashMovementResult.Failure(CashMovementResultStatus.InsufficientExpectedCash)),
        };
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = "1000";
        viewModel.ReasonText = "Motivo válido";

        var raised = false;
        viewModel.Confirmed += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.False(raised);
        Assert.Equal("El monto supera el efectivo esperado en caja.", viewModel.GeneralError);
    }

    [Fact]
    public async Task IsBusyIsTrueWhileRecordingAndFalseAfterCompletion()
    {
        var gate = new TaskCompletionSource<CashMovementResult>();
        var service = new FakeCashMovementService { RecordCashInHandler = _ => gate.Task };
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);
        viewModel.AmountText = "10";
        viewModel.ReasonText = "Motivo válido";

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(RecordCashMovementViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.ConfirmCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        gate.SetResult(CashMovementResult.SuccessResult(CreateEntry()));
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutCallingTheService()
    {
        var service = new FakeCashMovementService();
        var viewModel = CreateViewModel(service);
        viewModel.Load(CashMovementType.CashIn);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.GetCurrentSessionMovementsCallCount);
    }

    private static CashMovementEntry CreateEntry(CashMovementType type = CashMovementType.CashIn) =>
        new(Guid.NewGuid(), type, 10m, "MXN", "Motivo", Guid.NewGuid(), "Operador", DateTimeOffset.UtcNow);
}
