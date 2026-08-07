using Pos.Application.Authentication;
using Pos.Application.Inventory;
using Pos.Desktop.Inventory;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Inventory;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Inventory;

public class InventoryViewModelTests
{
    private static InventoryCatalogItem CreateItem(
        string sku = "SKU-001",
        string name = "Producto de prueba",
        decimal quantity = 10m,
        decimal reorderPoint = 2m,
        bool isActive = true,
        InventoryStockStatus stockStatus = InventoryStockStatus.InStock,
        ProductId? productId = null) =>
        new(productId ?? ProductId.New(), sku, "7501234567890", name, isActive, quantity, reorderPoint, stockStatus);

    private static FakeCurrentUserSession CreateSession(params Permission[] permissions)
    {
        var session = new FakeCurrentUserSession();

        if (permissions.Length > 0)
        {
            session.CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "JPEREZ", "Juan Pérez", "Gerente",
                permissions);
        }

        return session;
    }

    // ---------- Carga automática ----------

    [Fact]
    public async Task LoadCommandPopulatesItemsAndSummaryWithoutRequiringASearchTerm()
    {
        var item = CreateItem();
        var summary = new InventorySummary(10, 6, 3, 1);
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)),
            summaryHandler: _ => Task.FromResult(summary));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Items);
        Assert.Equal(10, viewModel.TrackedProductsCount);
        Assert.Equal(6, viewModel.InStockCount);
        Assert.Equal(3, viewModel.LowStockCount);
        Assert.Equal(1, viewModel.OutOfStockCount);
    }

    [Fact]
    public async Task LoadCommandWithNoResultsSetsTheNoResultsMessage()
    {
        var service = new FakeInventoryService();
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal("No se encontraron productos con inventario.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadCommandAlsoLoadsGlobalMovementHistory()
    {
        var movement = new InventoryMovementItem(
            InventoryMovementId.New(), ProductId.New(), "SKU-001", "Producto", InventoryMovementType.ManualIncrease,
            2m, 0m, 2m, null, DateTimeOffset.UtcNow);
        var service = new FakeInventoryService(
            movementHandler: (_, _, _, _) => Task.FromResult(new InventoryMovementPageResult([movement], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Movements);
    }

    // ---------- Search-as-you-type / debounce / stale response ----------

    [Fact]
    public async Task SettingSearchTextSchedulesADebouncedSearchThatReloadsTheCatalog()
    {
        var item = CreateItem();
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Items);
        Assert.Equal("agua", service.LastSearchTerm);
    }

    [Fact]
    public async Task ANewerSearchDiscardsAStaleCatalogResponseThatFinishesLater()
    {
        var oldQueryStarted = new TaskCompletionSource();
        var oldQueryResult = new TaskCompletionSource<InventoryCatalogPageResult>();
        var service = new FakeInventoryService(catalogHandler: (term, _, _, _, _) =>
        {
            if (term == "a")
            {
                oldQueryStarted.TrySetResult();
                return oldQueryResult.Task;
            }

            return Task.FromResult(new InventoryCatalogPageResult([CreateItem("SKU-AG")], false));
        });
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "a";
        var staleTask = viewModel.PendingSearchTask;
        await oldQueryStarted.Task;

        viewModel.SearchText = "ag";
        await viewModel.PendingSearchTask;

        Assert.Equal("SKU-AG", viewModel.Items.Single().Item.Sku);

        oldQueryResult.SetResult(new InventoryCatalogPageResult([CreateItem("SKU-OLD-A")], false));
        await staleTask;

        Assert.Equal("SKU-AG", viewModel.Items.Single().Item.Sku);
    }

    // ---------- Filtros / paginación ----------

    [Theory]
    [InlineData(InventoryCatalogStatusFilter.InStock)]
    [InlineData(InventoryCatalogStatusFilter.LowStock)]
    [InlineData(InventoryCatalogStatusFilter.OutOfStock)]
    public async Task ChangingTheSelectedStockFilterReloadsWithTheNewFilter(InventoryCatalogStatusFilter filter)
    {
        var service = new FakeInventoryService();
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));

        viewModel.SelectedStockFilter = filter;
        await Task.Yield();

        Assert.Equal(filter, service.LastFilter);
    }

    [Fact]
    public async Task ChangingTheFilterResetsToTheFirstPage()
    {
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([CreateItem()], true)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.SelectedStockFilter = InventoryCatalogStatusFilter.LowStock;
        await Task.Yield();

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(0, service.LastSkip);
    }

    [Fact]
    public async Task SearchingResetsToTheFirstPage()
    {
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([CreateItem()], true)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts), searchDebounceDelay: TimeSpan.Zero);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(0, service.LastSkip);
    }

    [Fact]
    public async Task NextPageCommandRequestsTheNextSkip()
    {
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([CreateItem()], true)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal(InventoryViewModel.PageSize, service.LastSkip);
        Assert.Equal(2, viewModel.CurrentPage);
    }

    // ---------- Selección tras refresh (sección 35/36) ----------

    [Fact]
    public async Task SelectionIsClearedWhenTheAdjustedItemNoLongerMatchesTheCurrentFilter()
    {
        var productId = ProductId.New();
        var callCount = 0;
        var service = new FakeInventoryService(catalogHandler: (_, filter, _, _, _) =>
        {
            callCount++;

            // Primera carga: el producto aparece en "Stock bajo". Tras el ajuste (refresh),
            // ya no cumple ese filtro y debe desaparecer.
            if (callCount == 1)
            {
                return Task.FromResult(new InventoryCatalogPageResult(
                    [CreateItem(productId: productId, quantity: 2m, reorderPoint: 5m, stockStatus: InventoryStockStatus.LowStock)], false));
            }

            return Task.FromResult(InventoryCatalogPageResult.Empty);
        });
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts, Permission.AdjustInventory));
        viewModel.SelectedStockFilter = InventoryCatalogStatusFilter.LowStock;
        await Task.Yield();
        await viewModel.PendingSearchTask;

        viewModel.SelectedItem = viewModel.Items.Single();
        Assert.NotNull(viewModel.SelectedItem);

        viewModel.ApplyInventoryAdjusted();
        await Task.Yield();

        Assert.Null(viewModel.SelectedItem);
        Assert.Empty(viewModel.Items);
    }

    // ---------- Ajustar existencia: permisos ----------

    [Fact]
    public void CanAdjustInventoryIsTrueOnlyWithAdjustInventoryPermission()
    {
        var withPermission = new InventoryViewModel(new FakeInventoryService(), CreateSession(Permission.AdjustInventory));
        var withoutPermission = new InventoryViewModel(new FakeInventoryService(), CreateSession(Permission.ManageProducts));

        Assert.True(withPermission.CanAdjustInventory);
        Assert.False(withoutPermission.CanAdjustInventory);
    }

    [Fact]
    public async Task AdjustCommandCannotExecuteWithoutAdjustInventoryPermission()
    {
        var item = CreateItem();
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.False(viewModel.AdjustCommand.CanExecute(viewModel.Items.Single()));
    }

    [Fact]
    public async Task AdjustCommandRaisesAdjustInventoryRequestedWithAdjustInventoryPermission()
    {
        var item = CreateItem();
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts, Permission.AdjustInventory));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        InventoryCatalogItem? raised = null;
        viewModel.AdjustInventoryRequested += (_, requestedItem) => raised = requestedItem;

        Assert.True(viewModel.AdjustCommand.CanExecute(viewModel.Items.Single()));
        viewModel.AdjustCommand.Execute(viewModel.Items.Single());

        Assert.Equal(item.ProductId, raised?.ProductId);
    }

    // ---------- Refresh tras ajuste exitoso ----------

    [Fact]
    public async Task ApplyInventoryAdjustedRefreshesTheCatalogPreservingTheCurrentPage()
    {
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([CreateItem()], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts, Permission.AdjustInventory));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        var callsBefore = service.GetCatalogPageCallCount;

        viewModel.ApplyInventoryAdjusted();
        await Task.Yield();

        Assert.True(service.GetCatalogPageCallCount > callsBefore);
        Assert.True(service.GetSummaryCallCount > 0);
    }

    [Fact]
    public async Task ApplyInventoryAdjustedRefreshesMovementsOnlyWhenTheMovementsTabIsSelected()
    {
        var service = new FakeInventoryService();
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts, Permission.AdjustInventory));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        var movementCallsAfterLoad = service.GetMovementPageCallCount;

        viewModel.ApplyInventoryAdjusted();
        await Task.Yield();

        Assert.Equal(movementCallsAfterLoad, service.GetMovementPageCallCount);

        viewModel.SelectedTabIndex = 1;
        viewModel.ApplyInventoryAdjusted();
        await Task.Yield();

        Assert.True(service.GetMovementPageCallCount > movementCallsAfterLoad);
    }

    // ---------- Ver movimientos ----------

    [Fact]
    public async Task ViewMovementsCommandSwitchesToTheMovementsTabFilteredByThatProduct()
    {
        var item = CreateItem(sku: "SKU-042");
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        viewModel.ViewMovementsCommand.Execute(viewModel.Items.Single());
        await Task.Yield();

        Assert.Equal(1, viewModel.SelectedTabIndex);
        Assert.True(viewModel.HasMovementProductFilter);
        Assert.Contains("SKU-042", viewModel.MovementProductFilterLabel);
        Assert.Equal(item.ProductId, service.LastMovementFilter?.ProductId);
    }

    [Fact]
    public async Task ClearMovementProductFilterCommandRemovesTheProductFilter()
    {
        var item = CreateItem();
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => Task.FromResult(new InventoryCatalogPageResult([item], false)));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;
        viewModel.ViewMovementsCommand.Execute(viewModel.Items.Single());
        await Task.Yield();

        viewModel.ClearMovementProductFilterCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.HasMovementProductFilter);
        Assert.Null(service.LastMovementFilter?.ProductId);
    }

    // ---------- Errores ----------

    [Fact]
    public async Task AnUnexpectedExceptionSetsAFriendlyGeneralErrorInsteadOfThrowing()
    {
        var service = new FakeInventoryService(
            catalogHandler: (_, _, _, _, _) => throw new InvalidOperationException("boom"));
        var viewModel = new InventoryViewModel(service, CreateSession(Permission.ManageProducts));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ocurrió un error inesperado.", viewModel.GeneralError);
    }
}
