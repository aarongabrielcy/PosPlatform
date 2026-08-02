using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Products.ManageProduct;
using Pos.Application.SalesCart;
using Pos.Desktop.Products;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Products;

public class EditProductViewModelTests
{
    private static EditProductViewModel CreateViewModel(
        FakeProductManagementService service, FakeCurrentSalesCart? currentSalesCart = null) =>
        new(service, currentSalesCart ?? new FakeCurrentSalesCart(), NullLogger<EditProductViewModel>.Instance);

    private static ProductDetails CreateDetails(
        ProductId? productId = null,
        string sku = "SKU-001",
        string? barcode = "7501234567890",
        string name = "Producto de prueba",
        bool tracksInventory = true,
        bool isActive = true,
        decimal currentQuantity = 8m,
        decimal reorderPoint = 2m) =>
        new(
            productId ?? ProductId.New(), sku, barcode, name, "Descripción", 10m, "MXN", 5m,
            tracksInventory, isActive, currentQuantity, reorderPoint);

    private static async Task<EditProductViewModel> CreateLoadedViewModelAsync(
        FakeProductManagementService service, ProductDetails details, FakeCurrentSalesCart? currentSalesCart = null)
    {
        var viewModel = CreateViewModel(service, currentSalesCart);
        await viewModel.LoadAsync(details.ProductId);

        return viewModel;
    }

    [Fact]
    public async Task LoadAsyncPopulatesFieldsFromTheService()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));

        var viewModel = await CreateLoadedViewModelAsync(service, details);

        Assert.Equal("SKU-001", viewModel.Sku);
        Assert.Equal("7501234567890", viewModel.Barcode);
        Assert.Equal("Producto de prueba", viewModel.Name);
        Assert.True(viewModel.TracksInventory);
        Assert.Equal(8m, viewModel.CurrentQuantity);
        Assert.Equal("2", viewModel.ReorderPointText);
        Assert.True(viewModel.IsActive);
        Assert.Equal("Desactivar", viewModel.ToggleActiveButtonText);
    }

    [Fact]
    public async Task LoadAsyncFormatsSalePriceAndCostWithTwoDecimals()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));

        var viewModel = await CreateLoadedViewModelAsync(service, details);

        Assert.Equal("10.00", viewModel.SalePriceText);
        Assert.Equal("5.00", viewModel.CostText);
    }

    [Fact]
    public async Task LoadAsyncSetsGeneralErrorWhenProductIsNotFound()
    {
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(null));
        var viewModel = CreateViewModel(service);

        await viewModel.LoadAsync(ProductId.New());

        Assert.False(string.IsNullOrEmpty(viewModel.GeneralError));
    }

    [Fact]
    public async Task SaveCommandCannotExecuteBeforeLoadCompletes()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateViewModel(service);

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task SaveCommandRejectsBlankName()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.Name = "   ";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("El nombre es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task SaveCommandRejectsInvalidSalePrice()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.SalePriceText = "-5";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task SaveCommandSendsTheProductIdAndFieldsAsUpdateProductRequest()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(details)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.Name = "Nombre actualizado";
        viewModel.SalePriceText = "20";
        viewModel.ReorderPointText = "4";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, service.UpdateCallCount);
        Assert.Equal(details.ProductId, service.LastUpdateRequest!.ProductId);
        Assert.Equal("Nombre actualizado", service.LastUpdateRequest.Name);
        Assert.Equal(20m, service.LastUpdateRequest.SalePrice);
        Assert.Equal(4m, service.LastUpdateRequest.ReorderPoint);
    }

    // ---------- SKU editable (TAREA 24C) ----------

    [Fact]
    public async Task SaveCommandRejectsBlankSku()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.Sku = "   ";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("El SKU es obligatorio.", viewModel.GeneralError);
        Assert.Equal(0, service.UpdateCallCount);
    }

    [Fact]
    public async Task SaveCommandSendsTheEditedSkuTrimmed()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(details)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.Sku = "  SKU-002  ";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("SKU-002", service.LastUpdateRequest!.Sku);
    }

    [Fact]
    public async Task SaveCommandMapsDuplicateSkuStatusToErrorMessage()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.DuplicateSku)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);
        viewModel.Sku = "SKU-002";

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ya existe otro producto con ese SKU.", viewModel.GeneralError);
    }

    [Fact]
    public async Task SaveCommandRaisesCartWarningWhenSkuChangedAndProductIsInTheCurrentCart()
    {
        var details = CreateDetails(sku: "SKU-OLD");
        var updated = CreateDetails(productId: details.ProductId, sku: "SKU-NEW");
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(updated)));
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(details.ProductId, "SKU-OLD", details.Name, 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var viewModel = await CreateLoadedViewModelAsync(service, details, cart);
        viewModel.Sku = "SKU-NEW";

        var raised = false;
        viewModel.CartWarningRequested += (_, _) => raised = true;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.True(raised);
    }

    [Fact]
    public async Task SaveCommandDoesNotRaiseCartWarningWhenSkuIsUnchanged()
    {
        var details = CreateDetails(sku: "SKU-OLD");
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(details)));
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(details.ProductId, "SKU-OLD", details.Name, 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var viewModel = await CreateLoadedViewModelAsync(service, details, cart);

        var raised = false;
        viewModel.CartWarningRequested += (_, _) => raised = true;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.False(raised);
    }

    [Fact]
    public async Task SaveCommandDoesNotRaiseCartWarningWhenProductIsNotInTheCurrentCart()
    {
        var details = CreateDetails(sku: "SKU-OLD");
        var updated = CreateDetails(productId: details.ProductId, sku: "SKU-NEW");
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(updated)));
        var viewModel = await CreateLoadedViewModelAsync(service, details, new FakeCurrentSalesCart());
        viewModel.Sku = "SKU-NEW";

        var raised = false;
        viewModel.CartWarningRequested += (_, _) => raised = true;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.False(raised);
    }

    [Fact]
    public async Task SaveCommandDoesNotBlockEditingWhenProductIsInTheCurrentCart()
    {
        var details = CreateDetails(sku: "SKU-OLD");
        var updated = CreateDetails(productId: details.ProductId, sku: "SKU-NEW");
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(updated)));
        var cart = new FakeCurrentSalesCart();
        cart.SetSnapshot(new SalesCartSnapshot(
            [new SalesCartLine(details.ProductId, "SKU-OLD", details.Name, 1m, 10m, 10m, "MXN", 5m, true)], "MXN"));
        var viewModel = await CreateLoadedViewModelAsync(service, details, cart);
        viewModel.Sku = "SKU-NEW";

        var saved = false;
        viewModel.Saved += (_, _) => saved = true;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.True(saved);
        Assert.Equal(1, service.UpdateCallCount);
    }

    [Fact]
    public async Task SaveCommandDoesNotSendReorderPointWhenProductDoesNotTrackInventory()
    {
        var details = CreateDetails(tracksInventory: false, currentQuantity: 0m, reorderPoint: 0m);
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(details)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Null(service.LastUpdateRequest!.ReorderPoint);
    }

    [Fact]
    public async Task SaveCommandRaisesSavedOnSuccess()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.SuccessResult(details)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        var raised = false;
        viewModel.Saved += (_, _) => raised = true;

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.True(raised);
    }

    [Fact]
    public async Task SaveCommandMapsDuplicateBarcodeStatusToErrorMessage()
    {
        var details = CreateDetails();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => Task.FromResult(UpdateProductResult.Failure(UpdateProductResultStatus.DuplicateBarcode)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.SaveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ya existe otro producto con ese código de barras.", viewModel.GeneralError);
    }

    [Fact]
    public async Task SaveCommandSetsIsBusyDuringExecutionAndClearsItAfterwards()
    {
        var details = CreateDetails();
        var gate = new TaskCompletionSource<UpdateProductResult>();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => gate.Task);
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.SaveCommand.CanExecute(null));

        gate.SetResult(UpdateProductResult.SuccessResult(details));

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task DoubleSubmitWhileBusyOnlyCallsServiceOnce()
    {
        var details = CreateDetails();
        var gate = new TaskCompletionSource<UpdateProductResult>();
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            updateHandler: (_, _) => gate.Task);
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.SaveCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, service.UpdateCallCount);

        gate.SetResult(UpdateProductResult.SuccessResult(details));
    }

    [Fact]
    public async Task ToggleActiveCommandDeactivatesAnActiveProduct()
    {
        var details = CreateDetails(isActive: true);
        var deactivated = CreateDetails(productId: details.ProductId, isActive: false);
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            setActiveHandler: (_, _, _) => Task.FromResult(UpdateProductResult.SuccessResult(deactivated)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.ToggleActiveCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, service.SetActiveCallCount);
        Assert.False(service.LastSetActiveValue);
        Assert.False(viewModel.IsActive);
        Assert.Equal("Activar", viewModel.ToggleActiveButtonText);
    }

    [Fact]
    public async Task ToggleActiveCommandReactivatesAnInactiveProduct()
    {
        var details = CreateDetails(isActive: false);
        var activated = CreateDetails(productId: details.ProductId, isActive: true);
        var service = new FakeProductManagementService(
            getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details),
            setActiveHandler: (_, _, _) => Task.FromResult(UpdateProductResult.SuccessResult(activated)));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.ToggleActiveCommand.Execute(null);
        await Task.Yield();

        Assert.True(service.LastSetActiveValue);
        Assert.True(viewModel.IsActive);
    }

    [Fact]
    public async Task AdjustInventoryCommandCannotExecuteWhenProductDoesNotTrackInventory()
    {
        var details = CreateDetails(tracksInventory: false, currentQuantity: 0m, reorderPoint: 0m);
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        Assert.False(viewModel.AdjustInventoryCommand.CanExecute(null));
    }

    [Fact]
    public async Task AdjustInventoryCommandRaisesAdjustInventoryRequestedWhenProductTracksInventory()
    {
        var details = CreateDetails(tracksInventory: true);
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        var raised = false;
        viewModel.AdjustInventoryRequested += (_, _) => raised = true;

        viewModel.AdjustInventoryCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ApplyInventoryAdjustedUpdatesTheDisplayedQuantityWithoutCallingTheService()
    {
        var details = CreateDetails(tracksInventory: true, currentQuantity: 10m);
        var service = new FakeProductManagementService(getByIdHandler: (_, _) => Task.FromResult<ProductDetails?>(details));
        var viewModel = await CreateLoadedViewModelAsync(service, details);

        viewModel.ApplyInventoryAdjusted(15m);

        Assert.Equal(15m, viewModel.CurrentQuantity);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequested()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateViewModel(service);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.UpdateCallCount);
    }
}
