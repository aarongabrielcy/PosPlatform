using System.Linq;
using Pos.Application.Inventory;
using Pos.Application.Reports;
using Pos.Desktop.Reports;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using FakeClock = Pos.Desktop.Tests.Sales.History.FakeClock;

namespace Pos.Desktop.Tests.Reports;

// Comandos ejecutados de forma síncrona (Execute(null) sin await): FakeOperationalReportsService
// devuelve Task.FromResult ya completado y no hay SynchronizationContext instalado en xUnit, así
// que el cuerpo async del comando corre hasta el final antes de que Execute retorne (mismo criterio
// que MainWindowViewModelTests.NavigatingToSalesHistoryTriggersItsLoadCommand).
public class ReportsViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    // ---------- Filtro de fecha por defecto (sección 8) ----------

    [Fact]
    public void DefaultsFromAndToDateToToday()
    {
        var clock = new FakeClock(Now);
        var viewModel = new ReportsViewModel(new FakeOperationalReportsService(), clock);

        var today = Now.ToLocalTime().Date;

        Assert.Equal(today, viewModel.FromDate);
        Assert.Equal(today, viewModel.ToDate);
    }

    // ---------- LoadCommand carga las 6 áreas (sección 41-45) ----------

    [Fact]
    public void LoadCommandLoadsAllSixReportAreas()
    {
        var service = new FakeOperationalReportsService();
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);

        Assert.Equal(1, service.GetSalesSummaryCallCount);
        Assert.Equal(1, service.GetRegisterClosuresCallCount);
        Assert.Equal(1, service.GetCashMovementsCallCount);
        Assert.Equal(1, service.GetProductSalesCallCount);
        Assert.Equal(1, service.GetLowStockCallCount);
        Assert.Equal(1, service.GetOperatorActivityCallCount);
    }

    [Fact]
    public void LoadCommandAppliesSalesSummaryData()
    {
        var summary = new SalesSummaryReport(2, 500m, 200m, 300m, "MXN");
        var service = new FakeOperationalReportsService(salesSummary: summary);
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);

        Assert.Equal(2, viewModel.SummarySaleCount);
        Assert.Equal("500.00 MXN", viewModel.SummaryGrossSalesText);
        Assert.Equal("200.00 MXN", viewModel.SummaryCashSalesText);
        Assert.Equal("300.00 MXN", viewModel.SummaryCardSalesText);
        Assert.Null(viewModel.SalesSummaryStatusMessage);
    }

    // ---------- Estados vacíos (sección 51) ----------

    [Fact]
    public void LoadCommandShowsEmptyStateMessagesWhenThereIsNoData()
    {
        var viewModel = new ReportsViewModel(new FakeOperationalReportsService(), new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);

        Assert.Equal("No hay ventas en el período seleccionado.", viewModel.SalesSummaryStatusMessage);
        Assert.Equal("No hay cierres de caja en el período.", viewModel.RegisterClosuresStatusMessage);
        Assert.Equal("No hay movimientos de caja.", viewModel.CashMovementsStatusMessage);
        Assert.Equal("No hay ventas de productos en el período seleccionado.", viewModel.ProductSalesStatusMessage);
        Assert.Equal("No hay productos con stock bajo o sin existencia.", viewModel.LowStockStatusMessage);
        Assert.Equal("No hay actividad de operadores en el período seleccionado.", viewModel.OperatorActivityStatusMessage);
    }

    [Fact]
    public void LoadCommandPopulatesRegisterClosuresRowsWithoutEmptyMessage()
    {
        var closure = new RegisterClosureReportItem(
            RegisterSessionId.New(), "Caja 1", Now.AddHours(-2), Now, UserId.New(), "Ana Pérez",
            UserId.New(), "Ana Pérez", 500m, 200m, 300m, 500m, 100m, 40m, 760m, 750m, -10m, "MXN");
        var service = new FakeOperationalReportsService(closures: [closure]);
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);

        Assert.Single(viewModel.RegisterClosures);
        Assert.Null(viewModel.RegisterClosuresStatusMessage);
        Assert.Equal("760.00 MXN", viewModel.RegisterClosures[0].ExpectedCashText);
        Assert.Equal("-10.00 MXN", viewModel.RegisterClosures[0].DifferenceText);
    }

    [Fact]
    public void LoadCommandPopulatesLowStockUsingTheInventoryCatalogItem()
    {
        var item = new InventoryCatalogItem(
            ProductId.New(), "SKU-001", null, "Agua 1L", true, 1m, 5m, InventoryStockStatus.LowStock);
        var service = new FakeOperationalReportsService(lowStockItems: [item]);
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));

        viewModel.LoadCommand.Execute(null);

        Assert.Single(viewModel.LowStockItems);
        Assert.Equal("Stock bajo", viewModel.LowStockItems[0].StockStatusText);
        Assert.Null(viewModel.LowStockStatusMessage);
    }

    // ---------- Validación de rango de fechas (sección 23/46) ----------

    [Fact]
    public void SettingFromDateAfterToDateShowsDateRangeErrorAndDoesNotQuery()
    {
        var service = new FakeOperationalReportsService();
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));
        viewModel.LoadCommand.Execute(null);
        var callsAfterInitialLoad = service.GetRegisterClosuresCallCount;

        viewModel.ToDate = Now.ToLocalTime().Date.AddDays(-5);

        Assert.False(string.IsNullOrEmpty(viewModel.DateRangeError));
        Assert.Empty(viewModel.RegisterClosures);
        Assert.Equal(callsAfterInitialLoad, service.GetRegisterClosuresCallCount);
    }

    [Fact]
    public void ChangingFromDateTriggersAnImmediateReload()
    {
        var service = new FakeOperationalReportsService();
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));

        viewModel.FromDate = Now.ToLocalTime().Date.AddDays(-1);

        Assert.True(service.GetSalesSummaryCallCount >= 1);
    }

    // ---------- Cambiar de pestaña (sección 7/50) ----------

    [Fact]
    public void SelectedTabIndexCanBeChangedWithoutTriggeringAReload()
    {
        var service = new FakeOperationalReportsService();
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));
        var callsBefore = service.GetSalesSummaryCallCount;

        viewModel.SelectedTabIndex = 2;

        Assert.Equal(2, viewModel.SelectedTabIndex);
        Assert.Equal(callsBefore, service.GetSalesSummaryCallCount);
    }

    // ---------- Detalle de cierre de caja (sección 12) ----------

    [Fact]
    public void OpenClosureDetailCommandShowsTheDetailPanel()
    {
        var closure = new RegisterClosureReportItem(
            RegisterSessionId.New(), "Caja 1", Now.AddHours(-2), Now, UserId.New(), "Ana Pérez",
            UserId.New(), "Ana Pérez", 500m, 200m, 300m, 500m, 100m, 40m, 760m, 750m, -10m, "MXN");
        var service = new FakeOperationalReportsService(closures: [closure], closureDetail: closure);
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));
        viewModel.LoadCommand.Execute(null);
        var row = viewModel.RegisterClosures.Single();

        viewModel.OpenClosureDetailCommand.Execute(row);

        Assert.True(viewModel.IsShowingClosureDetail);
        Assert.False(viewModel.IsShowingClosureList);
        Assert.NotNull(viewModel.SelectedClosureDetail);
        Assert.Equal("760.00 MXN", viewModel.SelectedClosureDetail!.ExpectedCashText);
        Assert.Equal(1, service.GetRegisterClosureDetailCallCount);
    }

    [Fact]
    public void CloseClosureDetailCommandReturnsToTheList()
    {
        var closure = new RegisterClosureReportItem(
            RegisterSessionId.New(), "Caja 1", Now.AddHours(-2), Now, UserId.New(), "Ana Pérez",
            UserId.New(), "Ana Pérez", 500m, 200m, 300m, 500m, 100m, 40m, 760m, 750m, -10m, "MXN");
        var service = new FakeOperationalReportsService(closures: [closure], closureDetail: closure);
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));
        viewModel.LoadCommand.Execute(null);
        var row = viewModel.RegisterClosures.Single();
        viewModel.OpenClosureDetailCommand.Execute(row);

        viewModel.CloseClosureDetailCommand.Execute(null);

        Assert.False(viewModel.IsShowingClosureDetail);
        Assert.True(viewModel.IsShowingClosureList);
    }

    // ---------- Limpiar filtros ----------

    [Fact]
    public void ClearFiltersCommandResetsDatesToTodayAndReloads()
    {
        var service = new FakeOperationalReportsService();
        var viewModel = new ReportsViewModel(service, new FakeClock(Now));
        viewModel.FromDate = Now.ToLocalTime().Date.AddDays(-10);
        var callsBefore = service.GetSalesSummaryCallCount;

        viewModel.ClearFiltersCommand.Execute(null);

        Assert.Equal(Now.ToLocalTime().Date, viewModel.FromDate);
        Assert.Equal(Now.ToLocalTime().Date, viewModel.ToDate);
        Assert.True(service.GetSalesSummaryCallCount > callsBefore);
    }
}
