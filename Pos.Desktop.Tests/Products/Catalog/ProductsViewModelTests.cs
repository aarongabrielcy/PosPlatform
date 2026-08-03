using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Products.Catalog;
using Pos.Domain.Common.Identifiers;

namespace Pos.Desktop.Tests.Products.Catalog;

public class ProductsViewModelTests
{
    private static ProductCatalogItem CreateItem(
        string sku = "SKU-001",
        string name = "Producto de prueba",
        bool tracksInventory = true,
        decimal quantity = 10m,
        decimal reorderPoint = 2m,
        bool isActive = true) =>
        new(ProductId.New(), sku, "7501234567890", name, 10m, "MXN", tracksInventory, quantity, reorderPoint, isActive);

    // ---------- Carga automática (sección 9) ----------

    [Fact]
    public async Task LoadCommandPopulatesProductsWithoutRequiringASearchTerm()
    {
        var item = CreateItem();
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Products);
        Assert.Equal(1, service.GetCatalogPageCallCount);
        Assert.Equal(string.Empty, service.LastSearchTerm);
    }

    [Fact]
    public async Task LoadCommandWithNoResultsSetsTheNoResultsMessage()
    {
        var service = new FakeProductManagementService();
        var viewModel = new ProductsViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("No se encontraron productos.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadCommandWithResultsSetsTheShownCountMessage()
    {
        var items = new[] { CreateItem("SKU-001"), CreateItem("SKU-002") };
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult(items, false)));
        var viewModel = new ProductsViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("2 productos mostrados.", viewModel.StatusMessage);
    }

    // ---------- Búsqueda (SKU, barcode, nombre delegados al servicio) ----------

    [Fact]
    public async Task SearchCommandSendsTheCurrentSearchTextAndFilterToTheService()
    {
        var service = new FakeProductManagementService();
        var viewModel = new ProductsViewModel(service) { SearchText = "agua" };

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("agua", service.LastSearchTerm);
        Assert.Equal(ProductCatalogStatusFilter.All, service.LastFilter);
        Assert.Equal(0, service.LastSkip);
        Assert.Equal(ProductsViewModel.PageSize, service.LastTake);
    }

    [Fact]
    public async Task SearchCommandWithBlankTermStillListsTheCatalog()
    {
        var item = CreateItem();
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service) { SearchText = string.Empty };

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Products);
        Assert.Equal(string.Empty, service.LastSearchTerm);
    }

    // ---------- Filtros (sección 11) ----------

    [Theory]
    [InlineData(ProductCatalogStatusFilter.Active)]
    [InlineData(ProductCatalogStatusFilter.Inactive)]
    [InlineData(ProductCatalogStatusFilter.LowStock)]
    [InlineData(ProductCatalogStatusFilter.OutOfStock)]
    public async Task ChangingTheSelectedFilterReloadsWithTheNewFilter(ProductCatalogStatusFilter filter)
    {
        var service = new FakeProductManagementService();
        var viewModel = new ProductsViewModel(service);

        viewModel.SelectedFilter = filter;
        await Task.Yield();

        Assert.Equal(filter, service.LastFilter);
    }

    [Fact]
    public async Task ChangingTheFilterResetsToTheFirstPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.SelectedFilter = ProductCatalogStatusFilter.Active;
        await Task.Yield();

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(0, service.LastSkip);
    }

    // ---------- Paginación (sección 9) ----------

    [Fact]
    public async Task NextPageCommandCannotExecuteWhenThereIsNoNextPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], false)));
        var viewModel = new ProductsViewModel(service);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task NextPageCommandAdvancesTheSkipByPageSize()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(ProductsViewModel.PageSize, service.LastSkip);
    }

    [Fact]
    public void PreviousPageCommandCannotExecuteOnTheFirstPage()
    {
        var service = new FakeProductManagementService();
        var viewModel = new ProductsViewModel(service);

        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task PreviousPageCommandGoesBackToTheFirstPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();

        viewModel.PreviousPageCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(0, service.LastSkip);
    }

    // ---------- Selección / editar / nuevo ----------

    [Fact]
    public async Task LoadingASingleResultSelectsItAutomatically()
    {
        var item = CreateItem();
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service);

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(item, viewModel.SelectedProduct);
    }

    [Fact]
    public void EditProductCommandCannotExecuteWithoutAnItem()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService());

        Assert.False(viewModel.EditProductCommand.CanExecute(null));
    }

    [Fact]
    public void EditProductCommandRaisesEditProductRequestedWithTheItemProductId()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService());

        ProductId? raisedProductId = null;
        viewModel.EditProductRequested += (_, productId) => raisedProductId = productId;

        viewModel.EditProductCommand.Execute(item);

        Assert.Equal(item.ProductId, raisedProductId);
    }

    [Fact]
    public void NewProductCommandRaisesNewProductRequested()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService());

        var raised = false;
        viewModel.NewProductRequested += (_, _) => raised = true;

        viewModel.NewProductCommand.Execute(null);

        Assert.True(raised);
    }

    // ---------- Refresco tras crear/editar/ajustar inventario (sección 17, 19) ----------

    [Fact]
    public async Task ApplyProductCreatedSearchesByTheCreatedSku()
    {
        var item = CreateItem("SKU-NEW");
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service);

        viewModel.ApplyProductCreated("SKU-NEW");
        await Task.Yield();

        Assert.Equal("SKU-NEW", viewModel.SearchText);
        Assert.Equal("SKU-NEW", service.LastSearchTerm);
        Assert.Equal(item, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task ApplyProductUpdatedRefreshesTheCatalogWithTheEditedSku()
    {
        var item = CreateItem("SKU-EDITED");
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service);

        viewModel.ApplyProductUpdated("SKU-EDITED");
        await Task.Yield();

        Assert.Equal("SKU-EDITED", service.LastSearchTerm);
        Assert.Single(viewModel.Products);
    }

    // ---------- Ver detalle de auditoría (TAREA 24D, sección 32/33) ----------

    [Fact]
    public void ViewAuditDetailCommandCannotExecuteWithoutAnItem()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService());

        Assert.False(viewModel.ViewAuditDetailCommand.CanExecute(null));
    }

    [Fact]
    public void ViewAuditDetailCommandRaisesAuditRequestedWithTheSelectedItem()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService());

        ProductCatalogItem? raisedItem = null;
        viewModel.AuditRequested += (_, catalogItem) => raisedItem = catalogItem;

        viewModel.ViewAuditDetailCommand.Execute(item);

        Assert.Same(item, raisedItem);
    }
}
