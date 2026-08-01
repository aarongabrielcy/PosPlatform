using Pos.Application.Authentication;
using Pos.Application.RegisterSessions;
using Pos.Application.SalesCart;
using Pos.Desktop.Main;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Main;

public class MainWindowViewModelTests
{
    private static readonly DateTimeOffset OpenedAtUtc = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ExposesDisplayNameAndRoleNameFromTheCurrentSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.Equal("Ana Pérez", viewModel.DisplayName);
        Assert.Equal("Cajero", viewModel.RoleName);
    }

    [Fact]
    public void MissingSessionProducesEmptyDisplayNameAndRoleNameInsteadOfThrowing()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(string.Empty, viewModel.DisplayName);
        Assert.Equal(string.Empty, viewModel.RoleName);
    }

    [Fact]
    public void ExposesRegisterDataFromTheCurrentRegisterSession()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        Assert.True(viewModel.IsRegisterOpen);
        Assert.Equal("Caja 1", viewModel.RegisterName);
        Assert.Equal("Caja abierta", viewModel.RegisterStatusText);
        Assert.Contains("100", viewModel.RegisterOpeningAmountText);
        Assert.Contains("MXN", viewModel.RegisterOpeningAmountText);
    }

    [Fact]
    public void NoOpenRegisterSessionProducesEmptyRegisterDataInsteadOfThrowing()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        Assert.False(viewModel.IsRegisterOpen);
        Assert.Equal(string.Empty, viewModel.RegisterName);
        Assert.Equal(string.Empty, viewModel.RegisterStatusText);
        Assert.Equal(string.Empty, viewModel.RegisterOpeningAmountText);
    }

    [Fact]
    public void LogoutCommandClearsTheSessionWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(1, session.ClearCallCount);
        Assert.False(session.IsAuthenticated);
    }

    [Fact]
    public void LogoutCommandRaisesLogoutRequestedWhenNoRegisterIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.True(raised);
    }

    [Fact]
    public void LogoutCommandIsBlockedWhenARegisterSessionIsOpen()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        var raised = false;
        viewModel.LogoutRequested += (_, _) => raised = true;

        viewModel.LogoutCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal(0, session.ClearCallCount);
        Assert.True(session.IsAuthenticated);
        Assert.False(string.IsNullOrEmpty(viewModel.LogoutBlockedMessage));
    }

    [Fact]
    public void LoggingOutTwiceDoesNotThrow()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var viewModel = CreateViewModel(session: session);

        viewModel.LogoutCommand.Execute(null);
        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(2, session.ClearCallCount);
    }

    [Fact]
    public void LogoutCommandDoesNotClearTheCart()
    {
        var salesCartService = new FakeSalesCartService();
        var viewModel = CreateViewModel(salesCartService: salesCartService);

        viewModel.LogoutCommand.Execute(null);

        Assert.Equal(0, salesCartService.ClearCallCount);
    }

    [Fact]
    public void CloseRegisterCommandRaisesCloseRegisterRequested()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateAuthenticatedUser("Ana Pérez", "Cajero") };
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var viewModel = CreateViewModel(session: session, registerSession: registerSession);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.True(raised);
        Assert.Equal(0, registerSession.ClearCallCount);
    }

    [Fact]
    public void CloseRegisterCommandIsBlockedWhenTheCartHasLines()
    {
        var registerSession = new FakeCurrentRegisterSession { Current = CreateActiveRegisterSession() };
        var salesCartService = new FakeSalesCartService(
            addHandler: (_, _) => Task.FromResult(SalesCartResult.SuccessResult(CreateSnapshotWithOneLine())));
        var viewModel = CreateViewModel(registerSession: registerSession, salesCartService: salesCartService);
        viewModel.SelectedSearchResult = CreateSearchResult();
        viewModel.AddSelectedProductCommand.Execute(null);

        var raised = false;
        viewModel.CloseRegisterRequested += (_, _) => raised = true;

        viewModel.CloseRegisterCommand.Execute(null);

        Assert.False(raised);
        Assert.False(string.IsNullOrEmpty(viewModel.CloseRegisterBlockedMessage));
    }

    [Fact]
    public void ConstructorDoesNotAcceptAServiceProviderOrResolveWindows()
    {
        var constructorParameterTypes = typeof(MainWindowViewModel)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name);

        Assert.DoesNotContain("IServiceProvider", constructorParameterTypes);
    }

    // ---------- Búsqueda (sección 27) ----------

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

    // ---------- Mensajes de búsqueda (CORRECCIÓN UX FINAL TAREA 24A) ----------

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

    // ---------- Agregar / totales (sección 27) ----------

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

    // ---------- Aumentar / disminuir / eliminar (sección 27) ----------

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

    // ---------- Cancelar venta (sección 27) ----------

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

    // ---------- Nuevo producto (TAREA 24A.1) ----------

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

    private static MainWindowViewModel CreateViewModel(
        FakeCurrentUserSession? session = null,
        FakeCurrentRegisterSession? registerSession = null,
        FakeSalesCartService? salesCartService = null,
        FakeCurrentSalesCart? currentSalesCart = null) =>
        new(
            session ?? new FakeCurrentUserSession(),
            registerSession ?? new FakeCurrentRegisterSession(),
            salesCartService ?? new FakeSalesCartService(),
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

    private static ActiveRegisterSession CreateActiveRegisterSession() =>
        new(
            RegisterSessionId.New(),
            OrganizationId.New(),
            BranchId.New(),
            RegisterId.New(),
            "Caja 1",
            UserId.New(),
            "Ana Pérez",
            OpenedAtUtc,
            100m,
            "MXN");

    private static ProductSearchResult CreateSearchResult(bool isAvailable = true) =>
        new(ProductId.New(), "SKU-001", "Agua 1L", 10m, "MXN", isAvailable ? 5m : 0m, true);

    private static SalesCartLine CreateCartLine(decimal quantity = 1m) =>
        new(ProductId.New(), "SKU-001", "Agua 1L", quantity, 10m, 10m * quantity, "MXN", 5m, true);

    private static SalesCartSnapshot CreateSnapshotWithOneLine() =>
        new([CreateCartLine()], "MXN");
}
