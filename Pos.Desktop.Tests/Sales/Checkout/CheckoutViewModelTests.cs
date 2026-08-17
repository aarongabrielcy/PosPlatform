using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Desktop.Sales.Checkout;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Sales.Checkout;

public class CheckoutViewModelTests
{
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void LoadsTheTotalFromTheCurrentCart()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(125.50m));
        var viewModel = CreateViewModel(cart: cart);

        Assert.Contains("125.50", viewModel.TotalAmountText);
        Assert.Equal("MXN", viewModel.Currency);
    }

    [Fact]
    public void EmptyCashTenderedShowsValidationErrorWithoutCallingTheService()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(100m));
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "   ";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("El efectivo recibido es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    [Fact]
    public void InvalidDecimalShowsValidationErrorWithoutCallingTheService()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(100m));
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "no-es-un-numero";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("El efectivo recibido no es un valor válido.", viewModel.GeneralError);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    [Fact]
    public void CashBelowTotalShowsValidationErrorWithoutCallingTheService()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(100m));
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "50";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("El efectivo recibido es menor que el total.", viewModel.GeneralError);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    [Fact]
    public void ChangeTextIsEmptyWhileCashIsBelowTotal()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(100m));
        var viewModel = CreateViewModel(cart: cart);

        viewModel.CashTenderedText = "50";

        Assert.Equal(string.Empty, viewModel.ChangeText);
    }

    [Fact]
    public void ChangeTextReflectsCashAboveTotal()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(125.50m));
        var viewModel = CreateViewModel(cart: cart);

        viewModel.CashTenderedText = "200";

        Assert.Contains("74.50", viewModel.ChangeText);
    }

    // ---------- Selección de forma de pago (TAREA 25C) ----------

    [Fact]
    public void CashIsSelectedByDefault()
    {
        var viewModel = CreateViewModel(cart: CreateCartWithSnapshot(100m));

        Assert.True(viewModel.IsCashSelected);
        Assert.False(viewModel.IsCardSelected);
    }

    [Fact]
    public void SelectingCardDeselectsCashAndViceVersa()
    {
        var viewModel = CreateViewModel(cart: CreateCartWithSnapshot(100m));

        viewModel.IsCardSelected = true;

        Assert.True(viewModel.IsCardSelected);
        Assert.False(viewModel.IsCashSelected);

        viewModel.IsCashSelected = true;

        Assert.True(viewModel.IsCashSelected);
        Assert.False(viewModel.IsCardSelected);
    }

    [Fact]
    public void ChangeTextIsEmptyWhileCardIsSelectedEvenWithCashTenderedTextSet()
    {
        var viewModel = CreateViewModel(cart: CreateCartWithSnapshot(100m));
        viewModel.CashTenderedText = "200";
        viewModel.IsCardSelected = true;

        Assert.Equal(string.Empty, viewModel.ChangeText);
    }

    [Fact]
    public void CardWithBlankReferenceShowsValidationErrorWithoutCallingTheService()
    {
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, CreateCartWithSnapshot(100m));
        viewModel.IsCardSelected = true;
        viewModel.CardReferenceText = "   ";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("La referencia/autorización es obligatoria.", viewModel.GeneralError);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    [Fact]
    public void CardWithReferenceLongerThan100CharactersShowsValidationErrorWithoutCallingTheService()
    {
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, CreateCartWithSnapshot(100m));
        viewModel.IsCardSelected = true;
        viewModel.CardReferenceText = new string('A', 101);

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("La referencia/autorización no puede superar 100 caracteres.", viewModel.GeneralError);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    [Fact]
    public async Task ConfirmingAValidCardPaymentSendsTrimmedReferenceAndRaisesCheckoutCompleted()
    {
        var summary = CreateCardSummary(100m, "AUTH-0099");
        var service = new FakeCheckoutService((_, _) => Task.FromResult(CheckoutResult.SuccessResult(summary)));
        var viewModel = CreateViewModel(service, CreateCartWithSnapshot(100m));
        viewModel.IsCardSelected = true;
        viewModel.CardReferenceText = "  AUTH-0099  ";

        CheckoutSummary? received = null;
        viewModel.CheckoutCompleted += (_, s) => received = s;

        viewModel.ConfirmCommand.Execute(null);

        Assert.Same(summary, received);
        Assert.Equal(1, service.CheckoutCallCount);
        Assert.Equal(CheckoutPaymentMethod.Card, service.LastRequest!.PaymentMethod);
        Assert.Equal("AUTH-0099", service.LastRequest.CardReference);
        await Task.CompletedTask;
    }

    [Fact]
    public void CardCheckoutFailureFromServiceShowsTheMappedMessage()
    {
        var service = new FakeCheckoutService(
            (_, _) => Task.FromResult(CheckoutResult.Failure(CheckoutResultStatus.InvalidCardReference)));
        var viewModel = CreateViewModel(service, CreateCartWithSnapshot(100m));
        viewModel.IsCardSelected = true;
        viewModel.CardReferenceText = "AUTH-1";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal("La referencia/autorización es obligatoria.", viewModel.GeneralError);
    }

    [Fact]
    public async Task ConfirmingASuccessfulCheckoutRaisesCheckoutCompletedWithTheSummary()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(20m));
        var summary = CreateSummary(20m, 20m, 0m);
        var service = new FakeCheckoutService((_, _) => Task.FromResult(CheckoutResult.SuccessResult(summary)));
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "20";

        CheckoutSummary? received = null;
        viewModel.CheckoutCompleted += (_, s) => received = s;

        viewModel.ConfirmCommand.Execute(null);

        Assert.Same(summary, received);
        Assert.Equal(1, service.CheckoutCallCount);
        Assert.Equal(20m, service.LastRequest!.CashTendered);
        await Task.CompletedTask;
    }

    [Fact]
    public void FailedCheckoutShowsAMessageWithoutRaisingCheckoutCompleted()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(20m));
        var service = new FakeCheckoutService(
            (_, _) => Task.FromResult(CheckoutResult.Failure(CheckoutResultStatus.InsufficientStock)));
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "20";

        var raised = false;
        viewModel.CheckoutCompleted += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);

        Assert.False(raised);
        Assert.False(string.IsNullOrEmpty(viewModel.GeneralError));
    }

    [Fact]
    public void ProductChangedFailureShowsTheConservativeMessage()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(20m));
        var service = new FakeCheckoutService(
            (_, _) => Task.FromResult(CheckoutResult.ProductChangedResult(
                Guid.NewGuid(), CheckoutProductChangeReason.Price, "15.00")));
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "20";

        viewModel.ConfirmCommand.Execute(null);

        Assert.Equal(
            "El producto fue modificado desde que se agregó a la venta. Actualiza el producto en el carrito antes de cobrar.",
            viewModel.GeneralError);
    }

    [Fact]
    public async Task IsBusyIsTrueWhileCheckingOutAndFalseAfterCompletion()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(20m));
        var workSource = new TaskCompletionSource<CheckoutResult>();
        var service = new FakeCheckoutService((_, _) => workSource.Task);
        var viewModel = CreateViewModel(service, cart);
        viewModel.CashTenderedText = "20";

        var completionSignal = new TaskCompletionSource();
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CheckoutViewModel.IsBusy) && !viewModel.IsBusy)
            {
                completionSignal.TrySetResult();
            }
        };

        viewModel.ConfirmCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));

        workSource.SetResult(CheckoutResult.SuccessResult(CreateSummary(20m, 20m, 0m)));
        await completionSignal.Task;

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequestedWithoutCallingTheService()
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(20m));
        var service = new FakeCheckoutService();
        var viewModel = CreateViewModel(service, cart);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.CheckoutCallCount);
    }

    private static CheckoutViewModel CreateViewModel(
        FakeCheckoutService? service = null, FakeCurrentSalesCart? cart = null) =>
        new(
            service ?? new FakeCheckoutService(),
            cart ?? new FakeCurrentSalesCart(),
            NullLogger<CheckoutViewModel>.Instance);

    private static SalesCartSnapshot CreateSnapshot(decimal unitPrice) =>
        new([new SalesCartLine(ProductId.New(), "SKU-001", "Producto de prueba", 1m, unitPrice, unitPrice, "MXN", 10m, true)], "MXN");

    private static FakeCurrentSalesCart CreateCartWithSnapshot(decimal unitPrice)
    {
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(CreateSnapshot(unitPrice));

        return cart;
    }

    private static CheckoutSummary CreateSummary(decimal total, decimal cashTendered, decimal change) =>
        new(Guid.NewGuid(), CompletedAtUtc, total, "MXN", CheckoutPaymentMethod.Cash, cashTendered, change, null);

    private static CheckoutSummary CreateCardSummary(decimal total, string cardReference) =>
        new(Guid.NewGuid(), CompletedAtUtc, total, "MXN", CheckoutPaymentMethod.Card, 0m, 0m, cardReference);
}
