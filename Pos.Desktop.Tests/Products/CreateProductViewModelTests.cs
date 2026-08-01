using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Products.CreateProduct;
using Pos.Desktop.Products;

namespace Pos.Desktop.Tests.Products;

public class CreateProductViewModelTests
{
    private static CreateProductViewModel CreateViewModel(FakeCreateProductService service) =>
        new(service, NullLogger<CreateProductViewModel>.Instance);

    private static CreateProductViewModel CreateValidViewModel(FakeCreateProductService service)
    {
        var viewModel = CreateViewModel(service);
        viewModel.Sku = "SKU-001";
        viewModel.Name = "Producto de prueba";
        viewModel.SalePriceText = "10";

        return viewModel;
    }

    [Fact]
    public async Task SaveCommandRejectsBlankSku()
    {
        var service = new FakeCreateProductService();
        var viewModel = CreateValidViewModel(service);
        viewModel.Sku = "   ";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("El SKU es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Fact]
    public async Task SaveCommandRejectsBlankName()
    {
        var service = new FakeCreateProductService();
        var viewModel = CreateValidViewModel(service);
        viewModel.Name = "   ";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("El nombre es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Fact]
    public async Task SaveCommandRejectsInvalidSalePrice()
    {
        var service = new FakeCreateProductService();
        var viewModel = CreateValidViewModel(service);
        viewModel.SalePriceText = "-5";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Fact]
    public async Task SaveCommandRequiresInventoryFieldsOnlyWhenTracksInventoryIsChecked()
    {
        var service = new FakeCreateProductService(
            (_, _) => Task.FromResult(CreateProductResult.SuccessResult(
                Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001")));
        var viewModel = CreateValidViewModel(service);
        viewModel.TracksInventory = true;
        viewModel.InitialQuantityText = "";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("La existencia inicial es obligatoria.", viewModel.GeneralError);
        Assert.Equal(0, service.CreateCallCount);
    }

    [Fact]
    public async Task SaveCommandSendsExpectedRequestAndRaisesProductCreatedOnSuccess()
    {
        var service = new FakeCreateProductService(
            (_, _) => Task.FromResult(CreateProductResult.SuccessResult(
                Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001")));
        var viewModel = CreateValidViewModel(service);
        viewModel.Barcode = " 7501234567890 ";
        viewModel.Description = " Descripción ";
        viewModel.TracksInventory = true;
        viewModel.InitialQuantityText = "8";
        viewModel.ReorderPointText = "2";

        string? createdSku = null;
        viewModel.ProductCreated += (_, sku) => createdSku = sku;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("SKU-001", createdSku);
        Assert.Equal(1, service.CreateCallCount);
        Assert.Equal("SKU-001", service.LastRequest!.Sku);
        Assert.Equal("7501234567890", service.LastRequest.Barcode);
        Assert.Equal("Descripción", service.LastRequest.Description);
        Assert.True(service.LastRequest.TracksInventory);
        Assert.Equal(8m, service.LastRequest.InitialQuantity);
        Assert.Equal(2m, service.LastRequest.ReorderPoint);
    }

    [Fact]
    public async Task SaveCommandIgnoresInventoryFieldsWhenTracksInventoryIsUnchecked()
    {
        var service = new FakeCreateProductService(
            (_, _) => Task.FromResult(CreateProductResult.SuccessResult(
                Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001")));
        var viewModel = CreateValidViewModel(service);
        viewModel.TracksInventory = false;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, service.CreateCallCount);
        Assert.False(service.LastRequest!.TracksInventory);
        Assert.Equal(0m, service.LastRequest.InitialQuantity);
        Assert.Equal(0m, service.LastRequest.ReorderPoint);
    }

    [Fact]
    public void TogglingTracksInventoryOffClearsInventoryTextFields()
    {
        var service = new FakeCreateProductService();
        var viewModel = CreateValidViewModel(service);
        viewModel.TracksInventory = true;
        viewModel.InitialQuantityText = "8";
        viewModel.ReorderPointText = "2";

        viewModel.TracksInventory = false;

        Assert.Equal(string.Empty, viewModel.InitialQuantityText);
        Assert.Equal(string.Empty, viewModel.ReorderPointText);
    }

    [Fact]
    public async Task SaveCommandMapsDuplicateSkuStatusToErrorMessage()
    {
        var service = new FakeCreateProductService(
            (_, _) => Task.FromResult(CreateProductResult.Failure(CreateProductResultStatus.DuplicateSku)));
        var viewModel = CreateValidViewModel(service);

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ya existe un producto con ese SKU.", viewModel.GeneralError);
    }

    [Fact]
    public void SaveCommandSetsIsBusyDuringExecutionAndClearsItAfterwards()
    {
        var gate = new TaskCompletionSource<CreateProductResult>();
        var service = new FakeCreateProductService((_, _) => gate.Task);
        var viewModel = CreateValidViewModel(service);

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        gate.SetResult(CreateProductResult.SuccessResult(Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001"));

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void DoubleSubmitWhileBusyOnlyCallsServiceOnce()
    {
        var gate = new TaskCompletionSource<CreateProductResult>();
        var service = new FakeCreateProductService((_, _) => gate.Task);
        var viewModel = CreateValidViewModel(service);

        viewModel.SaveCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, service.CreateCallCount);

        gate.SetResult(CreateProductResult.SuccessResult(Pos.Domain.Common.Identifiers.ProductId.New(), "SKU-001"));

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequested()
    {
        var service = new FakeCreateProductService();
        var viewModel = CreateViewModel(service);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.CreateCallCount);
    }
}
