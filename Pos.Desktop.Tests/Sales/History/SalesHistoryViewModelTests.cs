using Pos.Application.Authentication;
using Pos.Application.Receipts;
using Pos.Application.Sales.History;
using Pos.Desktop.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Sales;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.Sales.History;

public class SalesHistoryViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 7, 15, 0, 0, TimeSpan.Zero);

    private static SalesHistoryItem CreateItem(
        SaleId? saleId = null,
        DateTimeOffset? completedAtUtc = null,
        string cashierDisplayName = "Ana Pérez",
        string registerName = "Caja 1",
        decimal itemCount = 1m,
        decimal total = 10m,
        IReadOnlyList<SalesHistoryPaymentAmount>? paymentSummary = null) =>
        new(
            saleId ?? SaleId.New(),
            completedAtUtc ?? Now,
            UserId.New(),
            cashierDisplayName,
            RegisterId.New(),
            registerName,
            RegisterSessionId.New(),
            itemCount,
            total,
            "MXN",
            paymentSummary ?? [new SalesHistoryPaymentAmount(PaymentMethod.Cash, total)]);

    // ---------- Fecha por defecto (sección 11/45) ----------

    [Fact]
    public void ConstructorDefaultsFromAndToDateToTodayAccordingToTheInjectedClock()
    {
        var viewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.Equal(Now.ToLocalTime().Date, viewModel.FromDate);
        Assert.Equal(Now.ToLocalTime().Date, viewModel.ToDate);
    }

    // ---------- Carga inicial (LoadCommand) ----------

    [Fact]
    public async Task LoadCommandPopulatesItemsAndSummary()
    {
        var item = CreateItem();
        var service = new FakeSalesHistoryService(
            searchPageHandler: (_, _, _, _) => Task.FromResult(new SalesHistoryPageResult([item], false)),
            summaryHandler: (_, _) => Task.FromResult(new SalesHistorySummary(
                1, 10m, "MXN", [new SalesHistoryPaymentAmount(PaymentMethod.Cash, 10m)])));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Items);
        Assert.Equal(1, viewModel.SummaryCount);
        Assert.Contains("10.00", viewModel.SummaryTotalText);
    }

    [Fact]
    public async Task LoadCommandWithNoResultsSetsTheEmptyStateMessage()
    {
        var viewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal("No se encontraron ventas para los filtros seleccionados.", viewModel.StatusMessage);
        Assert.Equal(0, viewModel.SummaryCount);
    }

    [Fact]
    public async Task LoadCommandLoadsFilterOptionsOnlyOnce()
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal(1, service.GetFilterOptionsCallCount);
    }

    [Fact]
    public async Task LoadCommandPopulatesCashierAndRegisterOptionsWithATodosEntryFirst()
    {
        var cashier = new SalesHistoryCashierOption(UserId.New(), "Ana Pérez");
        var register = new SalesHistoryRegisterOption(RegisterId.New(), "Caja 1");
        var service = new FakeSalesHistoryService(
            filterOptionsHandler: _ => Task.FromResult(new SalesHistoryFilterOptions([cashier], [register])));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal(2, viewModel.CashierOptions.Count);
        Assert.Equal("Todos", viewModel.CashierOptions[0].DisplayName);
        Assert.Null(viewModel.CashierOptions[0].UserId);
        Assert.Equal("Ana Pérez", viewModel.CashierOptions[1].DisplayName);

        Assert.Equal(2, viewModel.RegisterOptions.Count);
        Assert.Equal("Todos", viewModel.RegisterOptions[0].Name);
        Assert.Equal("Caja 1", viewModel.RegisterOptions[1].Name);
    }

    // ---------- Debounce / stale response (TAREA 24F, sección 3-4, aplicado a 25B sección 15) ----------

    [Fact]
    public async Task SettingSearchTextSchedulesADebouncedSearchThatReloadsTheList()
    {
        var item = CreateItem();
        var service = new FakeSalesHistoryService(
            searchPageHandler: (_, _, _, _) => Task.FromResult(new SalesHistoryPageResult([item], false)));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        Assert.Single(viewModel.Items);
        Assert.Equal("agua", service.LastFilter?.SearchTerm);
    }

    [Fact]
    public async Task ANewerSearchDiscardsAStaleResponseThatFinishesLater()
    {
        var oldQueryStarted = new TaskCompletionSource();
        var oldQueryResult = new TaskCompletionSource<SalesHistoryPageResult>();
        var service = new FakeSalesHistoryService(searchPageHandler: (filter, _, _, _) =>
        {
            if (filter.SearchTerm == "a")
            {
                oldQueryStarted.TrySetResult();
                return oldQueryResult.Task;
            }

            return Task.FromResult(new SalesHistoryPageResult([CreateItem(registerName: "Caja NUEVA")], false));
        });
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "a";
        var staleTask = viewModel.PendingSearchTask;
        await oldQueryStarted.Task;

        viewModel.SearchText = "ag";
        await viewModel.PendingSearchTask;

        Assert.Equal("Caja NUEVA", viewModel.Items.Single().RegisterName);

        oldQueryResult.SetResult(new SalesHistoryPageResult([CreateItem(registerName: "Caja VIEJA")], false));
        await staleTask;

        Assert.Equal("Caja NUEVA", viewModel.Items.Single().RegisterName);
    }

    // Cambiar un filtro inmediato (Desde/Hasta/Cajero/Caja/Método) debe cancelar un debounce de
    // texto pendiente, no solo al revés (regresión: ambos caminos comparten el mismo CTS).
    [Fact]
    public async Task ChangingAnImmediateFilterCancelsAPendingDebouncedTextSearch()
    {
        var debounceStarted = new TaskCompletionSource();
        var debounceResult = new TaskCompletionSource<SalesHistoryPageResult>();
        var service = new FakeSalesHistoryService(searchPageHandler: (filter, _, _, _) =>
        {
            // El filtro inmediato agrega PaymentMethod, distinguiéndolo del filtro del debounce
            // (SearchTerm="a" sin PaymentMethod): así cada llamada del fake resuelve una respuesta
            // distinta, igual que ocurriría con dos términos de búsqueda distintos.
            if (filter.PaymentMethod is null)
            {
                debounceStarted.TrySetResult();
                return debounceResult.Task;
            }

            return Task.FromResult(new SalesHistoryPageResult([CreateItem(registerName: "Caja FILTRO")], false));
        });
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now), searchDebounceDelay: TimeSpan.Zero);

        viewModel.SearchText = "a";
        var staleTask = viewModel.PendingSearchTask;
        await debounceStarted.Task;

        viewModel.SelectedPaymentMethod = PaymentMethod.Card;
        await viewModel.PendingSearchTask;

        Assert.Equal("Caja FILTRO", viewModel.Items.Single().RegisterName);

        debounceResult.SetResult(new SalesHistoryPageResult([CreateItem(registerName: "Caja VIEJA")], false));
        await staleTask;

        Assert.Equal("Caja FILTRO", viewModel.Items.Single().RegisterName);
    }

    // ---------- Filtros inmediatos / página 1 ----------

    [Theory]
    [InlineData(nameof(SalesHistoryViewModel.FromDate))]
    [InlineData(nameof(SalesHistoryViewModel.ToDate))]
    public async Task ChangingADateFilterResetsToPageOneAndSearchesImmediately(string property)
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        await viewModel.PendingSearchTask;

        if (property == nameof(SalesHistoryViewModel.FromDate))
        {
            viewModel.FromDate = Now.ToLocalTime().Date.AddDays(-1);
        }
        else
        {
            viewModel.ToDate = Now.ToLocalTime().Date.AddDays(1);
        }

        await viewModel.PendingSearchTask;

        Assert.Equal(1, service.SearchPageCallCount);
        Assert.Equal(1, viewModel.CurrentPage);
    }

    [Fact]
    public async Task ChangingSelectedPaymentMethodSearchesImmediatelyWithoutDebounce()
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now), searchDebounceDelay: TimeSpan.FromSeconds(30));

        viewModel.SelectedPaymentMethod = PaymentMethod.Card;
        await viewModel.PendingSearchTask;

        Assert.Equal(PaymentMethod.Card, service.LastFilter?.PaymentMethod);
    }

    [Fact]
    public async Task ChangingSelectedCashierAppliesItsUserIdToTheFilter()
    {
        var cashierId = UserId.New();
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        viewModel.SelectedCashier = new CashierFilterOption(cashierId, "Ana Pérez");
        await viewModel.PendingSearchTask;

        Assert.Equal(cashierId, service.LastFilter?.CashierUserId);
    }

    // ---------- Validación de rango de fechas (sección 47) ----------

    [Fact]
    public async Task FromDateAfterToDateShowsAnInlineErrorAndDoesNotQuery()
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        await viewModel.PendingSearchTask;
        var callsBeforeInvalidRange = service.SearchPageCallCount;

        viewModel.FromDate = Now.ToLocalTime().Date.AddDays(5);
        viewModel.ToDate = Now.ToLocalTime().Date;
        await viewModel.PendingSearchTask;

        Assert.Equal("La fecha inicial no puede ser posterior a la fecha final.", viewModel.DateRangeError);
        Assert.Empty(viewModel.Items);
        Assert.Equal(callsBeforeInvalidRange, service.SearchPageCallCount);
    }

    // ---------- Paginación ----------

    [Fact]
    public async Task NextPageCommandAdvancesThePageAndComputesTheCorrectSkip()
    {
        var service = new FakeSalesHistoryService(
            searchPageHandler: (_, _, _, _) => Task.FromResult(new SalesHistoryPageResult([CreateItem()], true)));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;

        Assert.Equal(2, viewModel.CurrentPage);
        Assert.Equal(SalesHistoryViewModel.PageSize, service.LastSkip);
    }

    [Fact]
    public async Task PreviousPageCommandIsANoOpOnTheFirstPage()
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        await viewModel.PendingSearchTask;
        var callsBefore = service.SearchPageCallCount;

        viewModel.PreviousPageCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(1, viewModel.CurrentPage);
        Assert.Equal(callsBefore, service.SearchPageCallCount);
    }

    // ---------- Limpiar filtros (sección 46) ----------

    [Fact]
    public async Task ClearFiltersCommandRestoresDefaultsAndReloadsAtPageOne()
    {
        var service = new FakeSalesHistoryService();
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        viewModel.SearchText = "algo";
        viewModel.SelectedPaymentMethod = PaymentMethod.Card;
        await viewModel.PendingSearchTask;

        viewModel.ClearFiltersCommand.Execute(null);
        await viewModel.PendingSearchTask;

        Assert.Equal(string.Empty, viewModel.SearchText);
        Assert.Equal(Now.ToLocalTime().Date, viewModel.FromDate);
        Assert.Equal(Now.ToLocalTime().Date, viewModel.ToDate);
        Assert.Null(viewModel.SelectedPaymentMethod);
        Assert.Equal(CashierFilterOption.All, viewModel.SelectedCashier);
        Assert.Equal(RegisterFilterOption.All, viewModel.SelectedRegister);
        Assert.Equal(1, viewModel.CurrentPage);
    }

    // ---------- Detalle / LISTA-DETALLE (sección 22/32/36) ----------

    [Fact]
    public async Task OpenDetailCommandLoadsTheExactSaleAndSwitchesToDetailState()
    {
        var saleId = SaleId.New();
        var detail = new SaleHistoryDetail(
            saleId, SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN",
            [new SaleHistoryDetailLine("SKU-001", "Agua 1L", 1m, 10m, 10m, "MXN")],
            [new SaleHistoryDetailPayment(PaymentMethod.Cash, 10m, "MXN", Now, null)]);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        var row = new SalesHistoryRowViewModel(CreateItem(saleId: saleId));

        viewModel.OpenDetailCommand.Execute(row);
        await Task.Yield();

        Assert.True(viewModel.IsShowingDetail);
        Assert.False(viewModel.IsShowingList);
        Assert.NotNull(viewModel.SelectedSaleDetail);
        Assert.Equal(saleId, service.LastDetailSaleId);
    }

    [Fact]
    public async Task OpenDetailCommandShowsAnErrorAndStaysInTheListWhenTheServiceReturnsNull()
    {
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(null));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        var row = new SalesHistoryRowViewModel(CreateItem());

        viewModel.OpenDetailCommand.Execute(row);
        await Task.Yield();

        Assert.False(viewModel.IsShowingDetail);
        Assert.NotNull(viewModel.GeneralError);
    }

    // "Volver al historial" conserva filtros/página/búsqueda ya aplicados (sección 22/36): no
    // vuelve a consultar la base de datos ni los reinicia.
    [Fact]
    public async Task CloseDetailCommandReturnsToTheListPreservingFiltersAndPageWithoutRequerying()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;
        var callsBeforeDetail = service.SearchPageCallCount;

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();

        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.False(viewModel.IsShowingDetail);
        Assert.True(viewModel.IsShowingList);
        Assert.Equal("agua", viewModel.SearchText);
        Assert.Equal(callsBeforeDetail, service.SearchPageCallCount);
    }

    // ---------- Estado inicial LISTA/DETALLE (TAREA 25B-FIX, sección 2/16) ----------

    [Fact]
    public void InitialStateIsList()
    {
        var viewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.True(viewModel.IsShowingList);
        Assert.False(viewModel.IsShowingDetail);
    }

    [Fact]
    public void InitialSelectedDetailIsNull()
    {
        var viewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.Null(viewModel.SelectedSaleDetail);
    }

    // Reproduce la condición combinada exigida en 25B-FIX sección 4: IsShowingDetail nunca es true
    // sin SelectedSaleDetail. Aquí ambos parten en su estado por defecto (false/null), así que la
    // vista jamás debería renderizar la tarjeta "Detalle de venta" al abrir Historial por primera vez.
    [Fact]
    public void InitialViewDoesNotRenderDetail()
    {
        var viewModel = new SalesHistoryViewModel(new FakeSalesHistoryService(), new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.False(viewModel.IsShowingDetail && viewModel.SelectedSaleDetail is null);
        Assert.False(viewModel.IsShowingDetail);
    }

    [Fact]
    public async Task ListAndDetailVisibilityAreMutuallyExclusive()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.NotEqual(viewModel.IsShowingList, viewModel.IsShowingDetail);
        Assert.True(viewModel.IsShowingList);

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();

        Assert.NotEqual(viewModel.IsShowingList, viewModel.IsShowingDetail);
        Assert.True(viewModel.IsShowingDetail);

        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.NotEqual(viewModel.IsShowingList, viewModel.IsShowingDetail);
        Assert.True(viewModel.IsShowingList);
    }

    [Fact]
    public async Task BackReturnsToList()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();

        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.True(viewModel.IsShowingList);
        Assert.False(viewModel.IsShowingDetail);
        Assert.Null(viewModel.SelectedSaleDetail);
    }

    [Fact]
    public async Task BackPreservesDateFilters()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        var expectedFrom = Now.ToLocalTime().Date.AddDays(-3);
        var expectedTo = Now.ToLocalTime().Date.AddDays(-1);
        viewModel.FromDate = expectedFrom;
        viewModel.ToDate = expectedTo;
        await viewModel.PendingSearchTask;

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();
        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(expectedFrom, viewModel.FromDate);
        Assert.Equal(expectedTo, viewModel.ToDate);
    }

    [Fact]
    public async Task BackPreservesSearch()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now), searchDebounceDelay: TimeSpan.Zero);
        viewModel.SearchText = "agua";
        await viewModel.PendingSearchTask;

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();
        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("agua", viewModel.SearchText);
    }

    [Fact]
    public async Task BackPreservesDropdownFilters()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        var expectedCashier = new CashierFilterOption(UserId.New(), "Ana Pérez");
        var expectedRegister = new RegisterFilterOption(RegisterId.New(), "Caja 2");
        viewModel.SelectedCashier = expectedCashier;
        viewModel.SelectedRegister = expectedRegister;
        viewModel.SelectedPaymentMethod = PaymentMethod.Card;
        await viewModel.PendingSearchTask;

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();
        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(expectedCashier, viewModel.SelectedCashier);
        Assert.Equal(expectedRegister, viewModel.SelectedRegister);
        Assert.Equal(PaymentMethod.Card, viewModel.SelectedPaymentMethod);
    }

    [Fact]
    public async Task BackPreservesPage()
    {
        var detail = new SaleHistoryDetail(
            SaleId.New(), SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(
            searchPageHandler: (_, _, _, _) => Task.FromResult(new SalesHistoryPageResult([CreateItem()], true)),
            detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var viewModel = new SalesHistoryViewModel(service, new FakeCurrentUserSession(), new FakeReceiptPrintingService(), new FakeClock(Now));
        viewModel.LoadCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;
        viewModel.NextPageCommand.Execute(null);
        await Task.Yield();
        await viewModel.PendingSearchTask;
        Assert.Equal(2, viewModel.CurrentPage);

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem()));
        await Task.Yield();
        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();

        Assert.Equal(2, viewModel.CurrentPage);
    }

    // ---------- Reimprimir ticket (BASIC-PRN-01, sección 23/26/48/53 de la tarea) ----------

    private static AuthenticatedUser CreateUser(params Permission[] permissions) => new(
        UserId.New(), OrganizationId.New(), RoleId.New(), "JPEREZ", "Juan Pérez", "Cajero", permissions);

    [Fact]
    public void CanReprintIsFalseWithoutTheReprintReceiptPermission()
    {
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ViewSalesHistory) };
        var viewModel = new SalesHistoryViewModel(
            new FakeSalesHistoryService(), session, new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.False(viewModel.CanReprint);
    }

    [Fact]
    public void CanReprintIsTrueWithTheReprintReceiptPermission()
    {
        var session = new FakeCurrentUserSession
        {
            CurrentUser = CreateUser(Permission.ViewSalesHistory, Permission.ReprintReceipt),
        };
        var viewModel = new SalesHistoryViewModel(
            new FakeSalesHistoryService(), session, new FakeReceiptPrintingService(), new FakeClock(Now));

        Assert.True(viewModel.CanReprint);
    }

    [Fact]
    public async Task ReprintCommandOnSuccessSetsStatusMessageAndClearsAnyPreviousError()
    {
        var saleId = SaleId.New();
        var detail = new SaleHistoryDetail(
            saleId, SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var printingService = new FakeReceiptPrintingService { ReprintResultStatus = ReceiptPrintResultStatus.Success };
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ReprintReceipt) };
        var viewModel = new SalesHistoryViewModel(service, session, printingService, new FakeClock(Now));

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem(saleId: saleId)));
        await Task.Yield();

        viewModel.ReprintCommand.Execute(null);
        await Task.Yield();

        Assert.Equal("Ticket reimpreso.", viewModel.ReprintStatusMessage);
        Assert.Null(viewModel.ReprintErrorMessage);
        Assert.Equal(1, printingService.ReprintCallCount);
        Assert.Equal(saleId.Value, printingService.LastReprintSaleId);
    }

    [Fact]
    public async Task ReprintCommandOnPrinterFailureSetsErrorMessageWithoutClaimingTheSaleFailed()
    {
        var saleId = SaleId.New();
        var detail = new SaleHistoryDetail(
            saleId, SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var printingService = new FakeReceiptPrintingService { ReprintResultStatus = ReceiptPrintResultStatus.PrintFailed };
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ReprintReceipt) };
        var viewModel = new SalesHistoryViewModel(service, session, printingService, new FakeClock(Now));

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem(saleId: saleId)));
        await Task.Yield();

        viewModel.ReprintCommand.Execute(null);
        await Task.Yield();

        Assert.Null(viewModel.ReprintStatusMessage);
        Assert.NotNull(viewModel.ReprintErrorMessage);
        Assert.DoesNotContain("fallida", viewModel.ReprintErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OpeningDetailClearsAnyPreviousReprintMessages()
    {
        var saleId = SaleId.New();
        var detail = new SaleHistoryDetail(
            saleId, SaleStatus.Completed, Now, Now, UserId.New(), "Ana Pérez", RegisterId.New(), "Caja 1",
            RegisterSessionId.New(), 10m, 10m, "MXN", [], []);
        var service = new FakeSalesHistoryService(detailHandler: (_, _) => Task.FromResult<SaleHistoryDetail?>(detail));
        var printingService = new FakeReceiptPrintingService { ReprintResultStatus = ReceiptPrintResultStatus.Success };
        var session = new FakeCurrentUserSession { CurrentUser = CreateUser(Permission.ReprintReceipt) };
        var viewModel = new SalesHistoryViewModel(service, session, printingService, new FakeClock(Now));

        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem(saleId: saleId)));
        await Task.Yield();
        viewModel.ReprintCommand.Execute(null);
        await Task.Yield();
        Assert.NotNull(viewModel.ReprintStatusMessage);

        viewModel.CloseDetailCommand.Execute(null);
        await Task.Yield();
        viewModel.OpenDetailCommand.Execute(new SalesHistoryRowViewModel(CreateItem(saleId: saleId)));
        await Task.Yield();

        Assert.Null(viewModel.ReprintStatusMessage);
        Assert.Null(viewModel.ReprintErrorMessage);
    }
}
