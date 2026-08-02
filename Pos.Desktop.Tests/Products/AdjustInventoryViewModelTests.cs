using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Products;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;

namespace Pos.Desktop.Tests.Products;

public class AdjustInventoryViewModelTests
{
    private static AdjustInventoryViewModel CreateViewModel(FakeProductManagementService service) =>
        new(service, NullLogger<AdjustInventoryViewModel>.Instance);

    private static AdjustInventoryViewModel CreateLoadedViewModel(
        FakeProductManagementService service, ProductId? productId = null, decimal currentQuantity = 10m)
    {
        var viewModel = CreateViewModel(service);
        viewModel.Load(productId ?? ProductId.New(), "Producto de prueba", currentQuantity);

        return viewModel;
    }

    [Fact]
    public void LoadPopulatesProductNameAndCurrentQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 12m);

        Assert.Equal("Producto de prueba", viewModel.ProductName);
        Assert.Equal(12m, viewModel.CurrentQuantity);
    }

    [Fact]
    public void DifferenceTextShowsPositiveDifferenceWhenNewQuantityIsHigher()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.NewQuantityText = "15";

        Assert.Equal("+5", viewModel.DifferenceText);
    }

    [Fact]
    public void DifferenceTextShowsNegativeDifferenceWhenNewQuantityIsLower()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.NewQuantityText = "6";

        Assert.Equal("-4", viewModel.DifferenceText);
    }

    // TAREA 25A-FIX defecto 2: llevar la existencia exactamente a 0 debe funcionar sin que el
    // usuario tenga que calcular una magnitud de ajuste.
    [Fact]
    public void DifferenceTextShowsMinusCurrentQuantityWhenNewQuantityIsZero()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 2m);
        viewModel.NewQuantityText = "0";

        Assert.Equal("-2", viewModel.DifferenceText);
    }

    [Fact]
    public void DifferenceTextShowsPlaceholderForInvalidQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);
        viewModel.NewQuantityText = "abc";

        Assert.Equal("—", viewModel.DifferenceText);
    }

    [Fact]
    public async Task ConfirmCommandRejectsAnEmptyOrNonNumericNewQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);
        viewModel.NewQuantityText = "abc";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ConfirmCommandRejectsANegativeNewQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 3m);
        viewModel.NewQuantityText = "-1";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("La nueva existencia no puede ser negativa.", viewModel.GeneralError);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ConfirmCommandRejectsANewQuantityEqualToTheCurrentQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.NewQuantityText = "10";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("La nueva existencia debe ser diferente a la actual.", viewModel.GeneralError);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ConfirmCommandSendsAnIncreaseRequestWhenNewQuantityIsHigher()
    {
        var productId = ProductId.New();
        var service = new FakeProductManagementService(
            adjustInventoryHandler: (_, _) => Task.FromResult(AdjustProductInventoryResult.SuccessResult(productId, 15m)));
        var viewModel = CreateLoadedViewModel(service, productId, currentQuantity: 10m);
        viewModel.NewQuantityText = "15";

        var raised = false;
        viewModel.Confirmed += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.True(raised);
        Assert.Equal(1, service.AdjustInventoryCallCount);
        Assert.Equal(productId, service.LastAdjustInventoryRequest!.ProductId);
        Assert.Equal(InventoryAdjustmentType.Increase, service.LastAdjustInventoryRequest.AdjustmentType);
        Assert.Equal(5m, service.LastAdjustInventoryRequest.Quantity);
        Assert.Equal(15m, viewModel.ConfirmedNewQuantity);
    }

    // TAREA 25A-FIX defecto 2: el caso real que el usuario no pudo reproducir manualmente.
    [Fact]
    public async Task ConfirmCommandSendsADecreaseRequestThatReachesExactlyZero()
    {
        var productId = ProductId.New();
        var service = new FakeProductManagementService(
            adjustInventoryHandler: (_, _) => Task.FromResult(AdjustProductInventoryResult.SuccessResult(productId, 0m)));
        var viewModel = CreateLoadedViewModel(service, productId, currentQuantity: 2m);
        viewModel.NewQuantityText = "0";

        var raised = false;
        viewModel.Confirmed += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.True(raised);
        Assert.Equal(1, service.AdjustInventoryCallCount);
        Assert.Equal(InventoryAdjustmentType.Decrease, service.LastAdjustInventoryRequest!.AdjustmentType);
        Assert.Equal(2m, service.LastAdjustInventoryRequest.Quantity);
        Assert.Equal(0m, viewModel.ConfirmedNewQuantity);
    }

    [Fact]
    public async Task ConfirmCommandMapsResultingQuantityNegativeStatusToErrorMessage()
    {
        var service = new FakeProductManagementService(
            adjustInventoryHandler: (_, _) => Task.FromResult(
                AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ResultingQuantityNegative)));
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.NewQuantityText = "7";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("La existencia resultante no puede ser negativa.", viewModel.GeneralError);
    }

    [Fact]
    public void CancelCommandRaisesCancelRequested()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);

        var raised = false;
        viewModel.CancelRequested += (_, _) => raised = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public void ConfirmCommandSetsIsBusyDuringExecutionAndClearsItAfterwards()
    {
        var gate = new TaskCompletionSource<AdjustProductInventoryResult>();
        var service = new FakeProductManagementService(adjustInventoryHandler: (_, _) => gate.Task);
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.NewQuantityText = "11";

        viewModel.ConfirmCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));

        gate.SetResult(AdjustProductInventoryResult.SuccessResult(ProductId.New(), 11m));

        Assert.False(viewModel.IsBusy);
    }
}
