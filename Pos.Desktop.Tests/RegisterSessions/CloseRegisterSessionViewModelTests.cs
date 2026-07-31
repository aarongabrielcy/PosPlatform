using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Main;
using Pos.Desktop.RegisterSessions;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.RegisterSessions;

public class CloseRegisterSessionViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ClosedAtUtc = new(2026, 1, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoadsTheSummaryPreviewFromTheCurrentRegisterSession()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(new FakeRegisterSessionService(), registerSession);

        Assert.Equal("Caja 1", viewModel.RegisterName);
        Assert.Equal("Ana Pérez", viewModel.OpenedByDisplayName);
        Assert.Equal("MXN", viewModel.Currency);
        Assert.Contains("100", viewModel.OpeningAmountText);
        Assert.Contains("100", viewModel.ExpectedAmountText);
    }

    [Fact]
    public void EmptyClosingAmountShowsValidationErrorWithoutCallingTheService()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var service = new FakeRegisterSessionService();
        var viewModel = CreateViewModel(service, registerSession);
        viewModel.ClosingAmountText = "   ";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("El monto contado es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.CloseCallCount);
    }

    [Fact]
    public void InvalidDecimalShowsValidationErrorWithoutCallingTheService()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var service = new FakeRegisterSessionService();
        var viewModel = CreateViewModel(service, registerSession);
        viewModel.ClosingAmountText = "no-es-un-numero";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("El monto contado no es un valor válido.", viewModel.GeneralError);
        Assert.Equal(0, service.CloseCallCount);
    }

    [Fact]
    public void DifferenceTextReflectsTheEnteredClosingAmount()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession(openingAmount: 100m) };
        var viewModel = CreateViewModel(new FakeRegisterSessionService(), registerSession);

        viewModel.ClosingAmountText = "120";

        Assert.Contains("20", viewModel.DifferenceText);
    }

    [Fact]
    public async Task ConfirmingASuccessfulCloseRaisesRegisterClosedWithTheSummary()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var summary = CreateSummary();
        var service = new FakeRegisterSessionService(
            closeHandler: (_, _) => Task.FromResult(RegisterSessionResult.CloseSuccess(summary)));
        var viewModel = CreateViewModel(service, registerSession);
        viewModel.ClosingAmountText = "100";

        RegisterSessionSummary? received = null;
        viewModel.RegisterClosed += (_, s) => received = s;

        viewModel.ConfirmCommand.Execute(null);

        Assert.Same(summary, received);
        Assert.Equal(1, service.CloseCallCount);
        Assert.Equal(100m, service.LastCloseRequest!.ClosingAmount);
        await Task.CompletedTask;
    }

    [Fact]
    public void FailedCloseShowsAMessageWithoutRaisingRegisterClosed()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var service = new FakeRegisterSessionService(
            closeHandler: (_, _) =>
                Task.FromResult(RegisterSessionResult.Failure(RegisterSessionResultStatus.SessionAlreadyClosed)));
        var viewModel = CreateViewModel(service, registerSession);
        viewModel.ClosingAmountText = "100";

        var raised = false;
        viewModel.RegisterClosed += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("La caja ya fue cerrada.", viewModel.GeneralError);
    }

    [Fact]
    public async Task IsBusyIsTrueWhileClosingAndFalseAfterCompletion()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var workSource = new TaskCompletionSource<RegisterSessionResult>();
        var service = new FakeRegisterSessionService(closeHandler: (_, _) => workSource.Task);
        var viewModel = CreateViewModel(service, registerSession);
        viewModel.ClosingAmountText = "100";

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CloseRegisterSessionViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.ConfirmCommand.Execute(null);

        Assert.True(viewModel.IsBusy);

        workSource.SetResult(RegisterSessionResult.CloseSuccess(CreateSummary()));
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutCallingTheService()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var service = new FakeRegisterSessionService();
        var viewModel = CreateViewModel(service, registerSession);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.CloseCallCount);
    }

    private static CloseRegisterSessionViewModel CreateViewModel(
        FakeRegisterSessionService service, FakeCurrentRegisterSession registerSession) =>
        new(service, registerSession, NullLogger<CloseRegisterSessionViewModel>.Instance);

    private static ActiveRegisterSession CreateActiveRegisterSession(decimal openingAmount = 100m) =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            "Caja 1",
            UserId.New(),
            "Ana Pérez",
            OpenedAtUtc,
            openingAmount,
            "MXN");

    private static RegisterSessionSummary CreateSummary() =>
        new(
            "Caja 1", OpenedAtUtc, ClosedAtUtc, "Ana Pérez", "Ana Pérez",
            100m, 100m, 100m, 0m, "MXN");
}
