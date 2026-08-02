using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Desktop.Sales.Checkout;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Sales.Checkout;

public class CheckoutWindowTests
{
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

    // CheckoutWindow solo puede crearse y mostrarse (ShowDialog) en un hilo STA con bucle de
    // mensajes propio.
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }

    [Fact]
    public void SuccessfulCheckoutProducesDialogResultTrueAndExposesTheSummary() =>
        RunOnStaThread(() =>
        {
            var cart = new FakeCurrentSalesCart();
            cart.SetSnapshot(CreateSnapshot(20m));
            var summary = new CheckoutSummary(Guid.NewGuid(), CompletedAtUtc, 20m, "MXN", 20m, 0m);
            var service = new FakeCheckoutService((_, _) => Task.FromResult(CheckoutResult.SuccessResult(summary)));
            var viewModel = new CheckoutViewModel(service, cart, NullLogger<CheckoutViewModel>.Instance);
            var window = new CheckoutWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    viewModel.CashTenderedText = "20";
                    viewModel.ConfirmCommand.Execute(null);
                }));

            var dialogResult = window.ShowDialog();

            Assert.Equal(true, dialogResult);
            Assert.Same(summary, window.CompletedSummary);
        });

    [Fact]
    public void CancelCommandProducesDialogResultFalseWithoutASummary() =>
        RunOnStaThread(() =>
        {
            var cart = new FakeCurrentSalesCart();
            cart.SetSnapshot(CreateSnapshot(20m));
            var service = new FakeCheckoutService();
            var viewModel = new CheckoutViewModel(service, cart, NullLogger<CheckoutViewModel>.Instance);
            var window = new CheckoutWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => viewModel.CancelCommand.Execute(null)));

            var dialogResult = window.ShowDialog();

            Assert.Equal(false, dialogResult);
            Assert.Null(window.CompletedSummary);
            Assert.Equal(0, service.CheckoutCallCount);
        });

    private static SalesCartSnapshot CreateSnapshot(decimal unitPrice) =>
        new([new SalesCartLine(ProductId.New(), "SKU-001", "Producto de prueba", 1m, unitPrice, unitPrice, "MXN", 10m, true)], "MXN");
}
