using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Products;
using Pos.Domain.Common.Identifiers;

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
    public void IsIncreaseSelectedDefaultsToTrue()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);

        Assert.True(viewModel.IsIncreaseSelected);
        Assert.False(viewModel.IsDecreaseSelected);
    }

    [Fact]
    public void IsDecreaseSelectedIsTheMirrorOfIsIncreaseSelected()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);

        viewModel.IsDecreaseSelected = true;

        Assert.False(viewModel.IsIncreaseSelected);
        Assert.True(viewModel.IsDecreaseSelected);
    }

    [Fact]
    public void ResultingQuantityTextShowsIncreasedValue()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.IsIncreaseSelected = true;
        viewModel.QuantityText = "5";

        Assert.Equal("15", viewModel.ResultingQuantityText);
    }

    [Fact]
    public void ResultingQuantityTextShowsDecreasedValue()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.IsDecreaseSelected = true;
        viewModel.QuantityText = "4";

        Assert.Equal("6", viewModel.ResultingQuantityText);
    }

    [Fact]
    public void ResultingQuantityTextShowsPlaceholderForInvalidQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);
        viewModel.QuantityText = "abc";

        Assert.Equal("—", viewModel.ResultingQuantityText);
    }

    [Fact]
    public async Task ConfirmCommandRejectsZeroOrNegativeQuantity()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service);
        viewModel.QuantityText = "0";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ConfirmCommandRejectsADecreaseThatWouldGoNegativeBeforeCallingTheService()
    {
        var service = new FakeProductManagementService();
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 3m);
        viewModel.IsDecreaseSelected = true;
        viewModel.QuantityText = "5";

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.NotNull(viewModel.GeneralError);
        Assert.Equal(0, service.AdjustInventoryCallCount);
    }

    [Fact]
    public async Task ConfirmCommandSendsTheExpectedRequestAndRaisesConfirmedOnSuccess()
    {
        var productId = ProductId.New();
        var service = new FakeProductManagementService(
            adjustInventoryHandler: (_, _) => Task.FromResult(AdjustProductInventoryResult.SuccessResult(productId, 15m)));
        var viewModel = CreateLoadedViewModel(service, productId, currentQuantity: 10m);
        viewModel.IsIncreaseSelected = true;
        viewModel.QuantityText = "5";

        var raised = false;
        viewModel.Confirmed += (_, _) => raised = true;

        viewModel.ConfirmCommand.Execute(null);
        await Task.Yield();

        Assert.True(raised);
        Assert.Equal(1, service.AdjustInventoryCallCount);
        Assert.Equal(productId, service.LastAdjustInventoryRequest!.ProductId);
        Assert.Equal(5m, service.LastAdjustInventoryRequest.Quantity);
        Assert.Equal(15m, viewModel.ConfirmedNewQuantity);
    }

    [Fact]
    public async Task ConfirmCommandMapsResultingQuantityNegativeStatusToErrorMessage()
    {
        var service = new FakeProductManagementService(
            adjustInventoryHandler: (_, _) => Task.FromResult(
                AdjustProductInventoryResult.Failure(AdjustProductInventoryResultStatus.ResultingQuantityNegative)));
        var viewModel = CreateLoadedViewModel(service, currentQuantity: 10m);
        viewModel.IsDecreaseSelected = true;
        viewModel.QuantityText = "3";

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
        viewModel.QuantityText = "1";

        viewModel.ConfirmCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.ConfirmCommand.CanExecute(null));

        gate.SetResult(AdjustProductInventoryResult.SuccessResult(ProductId.New(), 11m));

        Assert.False(viewModel.IsBusy);
    }
}
