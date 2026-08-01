using System.Threading;
using System.Windows.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Products.CreateProduct;
using Pos.Desktop.Products;

namespace Pos.Desktop.Tests.Products;

public class CreateProductWindowTests
{
    // CreateProductWindow solo puede crearse y mostrarse (ShowDialog) en un hilo STA con bucle de
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

    private static CreateProductViewModel CreateViewModel(FakeCreateProductService service) =>
        new(service, NullLogger<CreateProductViewModel>.Instance);

    [Fact]
    public void CancelDoesNotCallServiceAndClosesWithDialogResultFalse() =>
        RunOnStaThread(() =>
        {
            var service = new FakeCreateProductService();
            var viewModel = CreateViewModel(service);
            var window = new CreateProductWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() => viewModel.CancelCommand.Execute(null)));

            var dialogResult = window.ShowDialog();

            Assert.Equal(false, dialogResult);
            Assert.Null(window.CreatedSku);
            Assert.Equal(0, service.CreateCallCount);
        });

    [Fact]
    public void SuccessfulSaveClosesWithDialogResultTrueAndExposesCreatedSku() =>
        RunOnStaThread(() =>
        {
            var service = new FakeCreateProductService(
                (_, _) => Task.FromResult(CreateProductResult.SuccessResult(
                    Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001")));
            var viewModel = CreateViewModel(service);
            var window = new CreateProductWindow(viewModel);

            window.Loaded += (_, _) => window.Dispatcher.BeginInvoke(
                DispatcherPriority.Background,
                new Action(() =>
                {
                    viewModel.Sku = "SKU-001";
                    viewModel.Name = "Producto de prueba";
                    viewModel.SalePriceText = "10";
                    viewModel.SaveCommand.Execute(null);
                }));

            var dialogResult = window.ShowDialog();

            Assert.Equal(true, dialogResult);
            Assert.Equal("SKU-001", window.CreatedSku);
        });
}
