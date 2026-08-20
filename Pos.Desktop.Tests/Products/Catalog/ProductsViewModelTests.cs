using Pos.Application.Authentication;
using Pos.Application.Products.ManageProduct;
using Pos.Desktop.Products.Catalog;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Products.Catalog;

public class ProductsViewModelTests
{
    // Sesión por defecto para las pruebas de este archivo (carga/búsqueda/paginación/comandos):
    // ManageProducts habilita New/Editar (CanExecute), igual patrón que las pruebas equivalentes
    // de InventoryViewModel/CanAdjustInventory. La gate READ-ONLY CORRECTION en sí (Cashier sin
    // ManageProducts) se prueba por separado más abajo.
    private static FakeCurrentUserSession CreateAuthorizedSession()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "GERENTE", "Ana Pérez", "Gerente",
                [Permission.ViewProducts, Permission.ManageProducts]),
        };

        return session;
    }

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("2 productos mostrados.", viewModel.StatusMessage);
    }

    // ---------- Búsqueda (SKU, barcode, nombre delegados al servicio) ----------

    [Fact]
    public async Task SearchCommandSendsTheCurrentSearchTextAndFilterToTheService()
    {
        var service = new FakeProductManagementService();
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession()) { SearchText = "agua" };

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession()) { SearchText = string.Empty };

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Products);
        Assert.Equal(string.Empty, service.LastSearchTerm);
    }

    // ---------- Search-as-you-type / debounce / race conditions (TAREA 24F) ----------

    [Fact]
    public async Task SettingSearchTextSchedulesADebouncedSearchThatReloadsTheCatalog()
    {
        var item = CreateItem();
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession(), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Products);
        Assert.Equal("agua", service.LastSearchTerm);
    }

    [Fact]
    public async Task ChangingSearchTextResetsToTheFirstPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession(), searchDebounceDelay: TimeSpan.Zero);
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(0, service.LastSkip);
    }

    [Fact]
    public async Task ANewerSearchDiscardsAStaleCatalogResponseThatFinishesLater()
    {
        var oldQueryStarted = new TaskCompletionSource();
        var oldQueryResult = new TaskCompletionSource<ProductCatalogPageResult>();
        var service = new FakeProductManagementService((term, _, _, _, _) =>
        {
            if (term == "a")
            {
                oldQueryStarted.TrySetResult();
                return oldQueryResult.Task;
            }

            return Task.FromResult(new ProductCatalogPageResult([CreateItem("SKU-AG")], false));
        });
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession(), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "a";
        var staleTask = viewModel.PendingSearchTask;
        await oldQueryStarted.Task;

        viewModel.SearchText = "ag";
        await viewModel.PendingSearchTask;

        Assert.Equal("SKU-AG", viewModel.Products.Single().Sku);

        // La respuesta de "a" llega después de que "ag" ya se aplicó: no debe reemplazar nada.
        oldQueryResult.SetResult(new ProductCatalogPageResult([CreateItem("SKU-OLD-A")], false));
        await staleTask;

        Assert.Equal("SKU-AG", viewModel.Products.Single().Sku);
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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

        viewModel.SelectedFilter = filter;
        await Task.Yield();

        Assert.Equal(filter, service.LastFilter);
    }

    [Fact]
    public async Task ChangingTheFilterResetsToTheFirstPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.NextPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task NextPageCommandAdvancesTheSkipByPageSize()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

        Assert.False(viewModel.PreviousPageCommand.CanExecute(null));
    }

    [Fact]
    public async Task PreviousPageCommandGoesBackToTheFirstPage()
    {
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([CreateItem()], true)));
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(item, viewModel.SelectedProduct);
    }

    [Fact]
    public async Task RefreshingKeepsTheSelectionWhenTheProductStillMatchesAfterEditing()
    {
        var editedId = ProductId.New();
        var service = new FakeProductManagementService((_, _, _, _, _) =>
        {
            var edited = new ProductCatalogItem(
                editedId, "SKU-EDITED", "7501234567890", "Coca-Cola 600ml", 10m, "MXN", true, 10m, 2m, true);
            var other = CreateItem("SKU-OTHER");

            return Task.FromResult(new ProductCatalogPageResult([edited, other], false));
        });
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        // LoadPageAsync solo auto-selecciona con un único resultado; con dos, se elige manualmente
        // como haría el usuario antes de editar.
        viewModel.SelectedProduct = viewModel.Products.Single(p => p.ProductId == editedId);

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(editedId, viewModel.SelectedProduct?.ProductId);
    }

    [Fact]
    public async Task RefreshingClearsTheSelectionWhenTheProductNoLongerMatchesTheFilter()
    {
        var productId = ProductId.New();
        var callCount = 0;
        var service = new FakeProductManagementService((_, _, _, _, _) =>
        {
            callCount++;

            if (callCount == 1)
            {
                var item = new ProductCatalogItem(productId, "SKU-1", null, "Producto", 10m, "MXN", true, 10m, 2m, true);
                var other = CreateItem("SKU-OTHER");

                return Task.FromResult(new ProductCatalogPageResult([item, other], false));
            }

            // Segunda carga: el producto seleccionado ya no aparece (p.ej. la edición lo sacó del
            // filtro/búsqueda actual).
            return Task.FromResult(new ProductCatalogPageResult([CreateItem("SKU-OTHER")], false));
        });
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        viewModel.SelectedProduct = viewModel.Products.Single(p => p.ProductId == productId);

        viewModel.SearchCommand.Execute(null);
        await Task.Yield();

        Assert.Null(viewModel.SelectedProduct);
    }

    [Fact]
    public void EditProductCommandCannotExecuteWithoutAnItem()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());

        Assert.False(viewModel.EditProductCommand.CanExecute(null));
    }

    [Fact]
    public void EditProductCommandRaisesEditProductRequestedWithTheItemProductId()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());

        ProductId? raisedProductId = null;
        viewModel.EditProductRequested += (_, productId) => raisedProductId = productId;

        viewModel.EditProductCommand.Execute(item);

        Assert.Equal(item.ProductId, raisedProductId);
    }

    [Fact]
    public void NewProductCommandRaisesNewProductRequested()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

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
        var viewModel = new ProductsViewModel(service, CreateAuthorizedSession());

        viewModel.ApplyProductUpdated("SKU-EDITED");
        await Task.Yield();

        Assert.Equal("SKU-EDITED", service.LastSearchTerm);
        Assert.Single(viewModel.Products);
    }

    // ---------- Ver detalle de auditoría (TAREA 24D, sección 32/33) ----------

    [Fact]
    public void ViewAuditDetailCommandCannotExecuteWithoutAnItem()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());

        Assert.False(viewModel.ViewAuditDetailCommand.CanExecute(null));
    }

    [Fact]
    public void ViewAuditDetailCommandRaisesAuditRequestedWithTheSelectedItem()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());

        ProductCatalogItem? raisedItem = null;
        viewModel.AuditRequested += (_, catalogItem) => raisedItem = catalogItem;

        viewModel.ViewAuditDetailCommand.Execute(item);

        Assert.Same(item, raisedItem);
    }

    // ---------- Solo lectura para Cashier (READ-ONLY CORRECTION, sección 8/16/24) ----------

    private static FakeCurrentUserSession CreateReadOnlySession()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), OrganizationId.New(), RoleId.New(), "CAJERO01", "Luis Cajero", "Cashier",
                [Permission.ProcessSale, Permission.ViewProducts]),
        };

        return session;
    }

    [Fact]
    public void CanManageProductsIsTrueOnlyWithManageProductsPermission()
    {
        var withManageProducts = new ProductsViewModel(new FakeProductManagementService(), CreateAuthorizedSession());
        var readOnly = new ProductsViewModel(new FakeProductManagementService(), CreateReadOnlySession());

        Assert.True(withManageProducts.CanManageProducts);
        Assert.False(readOnly.CanManageProducts);
    }

    [Fact]
    public void NewProductCommandCannotExecuteWithoutManageProductsPermission()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateReadOnlySession());

        Assert.False(viewModel.NewProductCommand.CanExecute(null));
    }

    [Fact]
    public void NewProductCommandDoesNotRaiseNewProductRequestedWithoutManageProductsPermission()
    {
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateReadOnlySession());

        var raised = false;
        viewModel.NewProductRequested += (_, _) => raised = true;

        viewModel.NewProductCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void EditProductCommandCannotExecuteWithoutManageProductsPermission()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateReadOnlySession());

        Assert.False(viewModel.EditProductCommand.CanExecute(item));
    }

    [Fact]
    public void EditProductCommandDoesNotRaiseEditProductRequestedWithoutManageProductsPermission()
    {
        var item = CreateItem();
        var viewModel = new ProductsViewModel(new FakeProductManagementService(), CreateReadOnlySession());

        ProductId? raisedProductId = null;
        viewModel.EditProductRequested += (_, productId) => raisedProductId = productId;

        viewModel.EditProductCommand.Execute(item);

        Assert.Null(raisedProductId);
    }

    [Fact]
    public async Task LoadCommandStillPopulatesProductsWithOnlyViewProductsPermission()
    {
        var item = CreateItem();
        var service = new FakeProductManagementService(
            (_, _, _, _, _) => Task.FromResult(new ProductCatalogPageResult([item], false)));
        var viewModel = new ProductsViewModel(service, CreateReadOnlySession());

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();

        Assert.Single(viewModel.Products);
    }
}
