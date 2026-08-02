using Pos.Application.Authentication;
using Pos.Application.SalesCart;
using Pos.Desktop.Sales;
using Pos.Desktop.Tests.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Sales;

public class SalesViewModelTests
{
    [Fact]
    public void CanManageProductsReflectsThePermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.True(viewModel.CanManageProducts);
    }

    [Fact]
    public void CanManageProductsIsFalseWithoutThePermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.False(viewModel.CanManageProducts);
    }

    // ---------- Búsqueda ----------

    [Fact]
    public void SearchCommandPopulatesSearchResultsFromTheService()
    {
        var searchResult = CreateSearchResult();
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([searchResult]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "agua";

        viewModel.SearchCommand.Execute(null);

        Assert.Single(viewModel.SearchResults);
        Assert.Equal(1, salesCartService.SearchCallCount);
        Assert.Equal("agua", salesCartService.LastSearchTerm);
    }

    [Fact]
    public void AddSelectedProductCommandCannotExecuteWithoutASelection()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.AddSelectedProductCommand.CanExecute(null));

        viewModel.SelectedSearchResult = CreateSearchResult();

        Assert.True(viewModel.AddSelectedProductCommand.CanExecute(null));
    }

    [Fact]
    public void AddSelectedProductCommandCannotExecuteWhenTheSelectionHasNoStock()
    {
        var viewModel = CreateViewModel();

        viewModel.SelectedSearchResult = CreateSearchResult(isAvailable: false);

        Assert.False(viewModel.AddSelectedProductCommand.CanExecute(null));
    }

    [Fact]
    public void SearchCommandWithBlankTermSetsTheHelpMessageInsteadOfSearching()
    {
        var salesCartService = new FakeSalesCartService();
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "   ";

        viewModel.SearchCommand.Execute(null);

        Assert.Equal("Escribe un SKU, código de barras o nombre para buscar.", viewModel.SearchStatusMessage);
        Assert.Equal(0, salesCartService.SearchCallCount);
    }

    [Fact]
    public void SearchCommandWithZeroResultsSetsTheNoResultsMessage()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "inexistente";

        viewModel.SearchCommand.Execute(null);

        Assert.Equal("No se encontraron productos.", viewModel.SearchStatusMessage);
    }

    [Fact]
    public void SearchCommandWithOneResultSetsTheSingularMessage()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "agua";

        viewModel.SearchCommand.Execute(null);

        Assert.Equal("1 producto encontrado.", viewModel.SearchStatusMessage);
    }

    [Fact]
    public void SearchCommandWithMultipleResultsSetsThePluralMessage()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>(
                [CreateSearchResult(), CreateSearchResult()]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "agua";

        viewModel.SearchCommand.Execute(null);

        Assert.Equal("2 productos encontrados.", viewModel.SearchStatusMessage);
    }

    [Fact]
    public void ChangingSearchTextClearsThePreviousStatusMessage()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SearchText = "agua";
        viewModel.SearchCommand.Execute(null);
        Assert.False(string.IsNullOrEmpty(viewModel.SearchStatusMessage));

        viewModel.SearchText = "agua c";

        Assert.Null(viewModel.SearchStatusMessage);
    }

    [Fact]
    public void AddingMoreThanAvailableStockSetsAGeneralErrorWithTheAvailableQuantity()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.InsufficientStock, 2m)));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Contains("2", viewModel.GeneralError);
        Assert.Empty(viewModel.CartLines);
    }

    [Fact]
    public void UpdatingAboveAvailableStockSetsAGeneralErrorWithTheAvailableQuantity()
    {
        var line = CreateCartLine(2m);
        var salesCartService = new FakeSalesCartService(
            updateHandler: (_, _) => Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.InsufficientStock, 2m)));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.IncreaseQuantityCommand.Execute(line);

        Assert.Contains("2", viewModel.GeneralError);
    }

    // ---------- Agregar / totales ----------

    [Fact]
    public void AddingAProductUpdatesCartLinesAndTotals()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(CreateSnapshotWithOneLine())));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Single(viewModel.CartLines);
        Assert.True(viewModel.HasItems);
        Assert.Contains("MXN", viewModel.GrandTotal);
        Assert.Equal(1, salesCartService.AddCallCount);
    }

    [Fact]
    public void AddingAnInactiveProductSetsGeneralErrorInsteadOfThrowing()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.ProductInactive)));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.GeneralError));
        Assert.Empty(viewModel.CartLines);
    }

    [Fact]
    public void AddingWithoutAnOpenRegisterSetsGeneralError()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.RegisterSessionRequired)));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.GeneralError));
    }

    [Fact]
    public void AddingWhileUnauthenticatedSetsGeneralError()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.Failure(SalesCartResultStatus.NotAuthenticated)));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.GeneralError));
    }

    [Fact]
    public void AddingWhileBusyIsIgnoredUntilTheFirstCallCompletes()
    {
        var tcs = new TaskCompletionSource<SalesCartResult>();
        var salesCartService = new FakeSalesCartService(addHandler: (_, _) => tcs.Task);
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.AddSelectedProductCommand.CanExecute(null));

        viewModel.AddSelectedProductCommand.Execute(null);

        Assert.Equal(1, salesCartService.AddCallCount);

        tcs.SetResult(SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN")));

        Assert.False(viewModel.IsBusy);
    }

    // ---------- Aumentar / disminuir / eliminar ----------

    [Fact]
    public void IncreaseQuantityCallsUpdateWithIncrementedQuantity()
    {
        var line = CreateCartLine(2m);
        var salesCartService = new FakeSalesCartService(
            updateHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN"))));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.IncreaseQuantityCommand.Execute(line);

        Assert.Equal(1, salesCartService.UpdateCallCount);
        Assert.Equal(3m, salesCartService.LastUpdateRequest!.NewQuantity);
        Assert.Equal(line.ProductId, salesCartService.LastUpdateRequest.ProductId);
    }

    [Fact]
    public void DecreaseQuantityCallsUpdateWithDecrementedQuantity()
    {
        var line = CreateCartLine(2m);
        var salesCartService = new FakeSalesCartService(
            updateHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN"))));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.DecreaseQuantityCommand.Execute(line);

        Assert.Equal(1, salesCartService.UpdateCallCount);
        Assert.Equal(1m, salesCartService.LastUpdateRequest!.NewQuantity);
    }

    [Fact]
    public void DecreaseQuantityDoesNothingWhenQuantityIsAlreadyOne()
    {
        var line = CreateCartLine(1m);
        var salesCartService = new FakeSalesCartService();
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        Assert.False(viewModel.DecreaseQuantityCommand.CanExecute(line));

        viewModel.DecreaseQuantityCommand.Execute(line);

        Assert.Equal(0, salesCartService.UpdateCallCount);
    }

    [Fact]
    public void RemoveLineCallsServiceWithTheLineProductId()
    {
        var line = CreateCartLine(1m);
        var salesCartService = new FakeSalesCartService(
            removeHandler: _ => SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN")));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.RemoveLineCommand.Execute(line);

        Assert.Equal(1, salesCartService.RemoveCallCount);
        Assert.Equal(line.ProductId, salesCartService.LastRemovedProductId);
    }

    // ---------- Cancelar venta ----------

    [Fact]
    public void CancelSaleDoesNothingWhenTheCartIsEmpty()
    {
        var viewModel = CreateViewModel();

        var raised = false;
        viewModel.CancelSaleConfirmationRequested += (_, _) => raised = true;

        viewModel.CancelSaleCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public void CancelSaleRaisesConfirmationRequestedWhenTheCartHasLines()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(CreateSnapshotWithOneLine())));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();
        viewModel.AddSelectedProductCommand.Execute(null);

        var raised = false;
        viewModel.CancelSaleConfirmationRequested += (_, _) => raised = true;

        viewModel.CancelSaleCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ConfirmCancelSaleClearsTheCartThroughTheService()
    {
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(CreateSnapshotWithOneLine())),
            clearHandler: () => SalesCartResult.SuccessResult(SalesCartSnapshot.Empty("MXN")));
        var viewModel = CreateViewModel(salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();
        viewModel.AddSelectedProductCommand.Execute(null);

        viewModel.ConfirmCancelSale();

        Assert.Equal(1, salesCartService.ClearCallCount);
        Assert.Empty(viewModel.CartLines);
        Assert.False(viewModel.HasItems);
    }

    // ---------- Nuevo producto ----------

    [Fact]
    public void NewProductCommandRaisesNewProductRequested()
    {
        var viewModel = CreateViewModel();

        var raised = false;
        viewModel.NewProductRequested += (_, _) => raised = true;

        viewModel.NewProductCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void ApplyProductCreatedSetsSearchTextAndRunsSearch()
    {
        var searchResult = CreateSearchResult();
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([searchResult]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.Equal("SKU-NEW", viewModel.SearchText);
        Assert.Equal(1, salesCartService.SearchCallCount);
        Assert.Equal("SKU-NEW", salesCartService.LastSearchTerm);
        Assert.Single(viewModel.SearchResults);
    }

    [Fact]
    public void ApplyProductCreatedNeverAddsTheProductToTheCart()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.ApplyProductCreated("SKU-NEW");

        Assert.Empty(viewModel.CartLines);
        Assert.Equal(0, salesCartService.AddCallCount);
    }

    // ---------- Editar producto / Incluir inactivos ----------

    [Fact]
    public void EditProductCommandCannotExecuteWithoutASelection()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);

        Assert.False(viewModel.EditProductCommand.CanExecute(null));

        viewModel.SelectedSearchResult = CreateSearchResult();

        Assert.True(viewModel.EditProductCommand.CanExecute(null));
    }

    [Fact]
    public void EditProductCommandCannotExecuteWithoutManageProductsPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);
        viewModel.SelectedSearchResult = CreateSearchResult();

        Assert.False(viewModel.EditProductCommand.CanExecute(null));
        Assert.False(viewModel.CanManageProducts);
    }

    [Fact]
    public void EditProductCommandRaisesEditProductRequestedWithTheSelectedProductId()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var viewModel = CreateViewModel(session: session);
        var searchResult = CreateSearchResult();
        viewModel.SelectedSearchResult = searchResult;

        ProductId? raisedProductId = null;
        viewModel.EditProductRequested += (_, productId) => raisedProductId = productId;

        viewModel.EditProductCommand.Execute(null);

        Assert.Equal(searchResult.ProductId, raisedProductId);
    }

    [Fact]
    public void SearchWithIncludeInactiveUsesTheManagementServiceWhenPermitted()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productManagementService = new FakeProductManagementService(
            searchHandler: (_, _, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var salesCartService = new FakeSalesCartService();
        var viewModel = CreateViewModel(session: session, salesCartService: salesCartService, productManagementService: productManagementService);
        viewModel.SearchText = "agua";

        viewModel.IncludeInactive = true;

        Assert.Equal(1, productManagementService.SearchCallCount);
        Assert.True(productManagementService.LastIncludeInactive);
        Assert.Equal(0, salesCartService.SearchCallCount);
    }

    [Fact]
    public void SearchWithoutIncludeInactiveUsesTheSalesCartService()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateManageProductsUser() };
        var productManagementService = new FakeProductManagementService();
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var viewModel = CreateViewModel(session: session, salesCartService: salesCartService, productManagementService: productManagementService);
        viewModel.SearchText = "agua";

        viewModel.SearchCommand.Execute(null);

        Assert.Equal(1, salesCartService.SearchCallCount);
        Assert.Equal(0, productManagementService.SearchCallCount);
    }

    [Fact]
    public void IncludeInactiveIsIgnoredWithoutManageProductsPermissionAndFallsBackToSalesCartService()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var productManagementService = new FakeProductManagementService();
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var viewModel = CreateViewModel(session: session, salesCartService: salesCartService, productManagementService: productManagementService);
        viewModel.SearchText = "agua";

        viewModel.IncludeInactive = true;

        Assert.Equal(0, productManagementService.SearchCallCount);
        Assert.Equal(1, salesCartService.SearchCallCount);
    }

    [Fact]
    public void ApplyProductUpdatedSetsSearchTextAndRunsSearch()
    {
        var salesCartService = new FakeSalesCartService(
            searchHandler: (_, _) => Task.FromResult<IReadOnlyList<ProductSearchResult>>([CreateSearchResult()]));
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.ApplyProductUpdated("SKU-EDITED");

        Assert.Equal("SKU-EDITED", viewModel.SearchText);
        Assert.Equal(1, salesCartService.SearchCallCount);
    }

    private static SalesViewModel CreateViewModel(
        FakeCurrentUserSession? session = null,
        FakeSalesCartService? salesCartService = null,
        FakeProductManagementService? productManagementService = null,
        FakeCurrentSalesCart? currentSalesCart = null) =>
        new(
            session ?? new FakeCurrentUserSession(),
            salesCartService ?? new FakeSalesCartService(),
            productManagementService ?? new FakeProductManagementService(),
            currentSalesCart ?? new FakeCurrentSalesCart());

    private static AuthenticatedUser CreateAuthenticatedUser(string displayName, string roleName) =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "USERNAME",
            displayName,
            roleName,
            [Permission.ProcessSale]);

    private static AuthenticatedUser CreateManageProductsUser() =>
        new(
            UserId.New(),
            OrganizationId.New(),
            RoleId.New(),
            "GERENTE",
            "Ana Pérez",
            "Gerente",
            [Permission.ProcessSale, Permission.ManageProducts]);

    private static ProductSearchResult CreateSearchResult(bool isAvailable = true) =>
        new(ProductId.New(), "SKU-001", "Agua 1L", 10m, "MXN", isAvailable ? 5m : 0m, true);

    private static SalesCartLine CreateCartLine(decimal quantity = 1m) =>
        new(ProductId.New(), "SKU-001", "Agua 1L", quantity, 10m, 10m * quantity, "MXN", 5m, true);

    private static SalesCartSnapshot CreateSnapshotWithOneLine() =>
        new([CreateCartLine()], "MXN");
}
