using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Common.Time;
using Pos.Application.Reports;
using Pos.Desktop.Common;
using Pos.Desktop.Inventory;

namespace Pos.Desktop.Reports;

// Reportes (BASIC-RPT-01): módulo administrativo de solo lectura, Manager/Admin únicamente
// (Permission.ViewReports, aplicado en OperationalReportsService - nunca solo en la UI, sección 40
// de la tarea). Un único ViewModel con 6 áreas seleccionables por pestaña (sección 7: "un módulo
// Reportes con tabs", no seis entradas de navegación separadas), igual criterio que InventoryViewModel
// (Existencias + Movimientos en un solo ViewModel). Desde/Hasta es el filtro común (sección 8);
// cambiar de pestaña nunca vuelve a consultar la base de datos porque LoadCommand ya cargó las 6
// áreas juntas (Low Stock aparte, sin rango de fechas - sección 16).
public sealed class ReportsViewModel : ViewModelBase
{
    private readonly IOperationalReportsService _reportsService;
    private readonly IClock _clock;

    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _clearFiltersCommand;
    private readonly AsyncRelayCommand<RegisterClosureRowViewModel> _openClosureDetailCommand;
    private readonly AsyncRelayCommand _closeClosureDetailCommand;

    private DateTime? _fromDate;
    private DateTime? _toDate;
    private int _selectedTabIndex;
    private bool _isBusy;
    private string? _dateRangeError;
    private string? _generalError;

    private int _summarySaleCount;
    private string _summaryGrossSalesText = "0.00";
    private string _summaryCashSalesText = "0.00";
    private string _summaryCardSalesText = "0.00";
    private string? _salesSummaryStatusMessage;

    private string? _registerClosuresStatusMessage;
    private RegisterClosureRowViewModel? _selectedClosure;
    private RegisterClosureDetailViewModel? _selectedClosureDetail;
    private bool _isShowingClosureDetail;

    private string _cashInTotalText = "0.00";
    private string _cashOutTotalText = "0.00";
    private string? _cashMovementsStatusMessage;

    private string? _productSalesStatusMessage;

    private string? _lowStockStatusMessage;

    private string? _operatorActivityStatusMessage;

    public ReportsViewModel(IOperationalReportsService reportsService, IClock clock)
    {
        _reportsService = reportsService ?? throw new ArgumentNullException(nameof(reportsService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        var today = _clock.UtcNow.ToLocalTime().Date;
        _fromDate = today;
        _toDate = today;

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, onError: HandleUnexpectedError);
        _clearFiltersCommand = new AsyncRelayCommand(ExecuteClearFiltersAsync, onError: HandleUnexpectedError);
        _openClosureDetailCommand = new AsyncRelayCommand<RegisterClosureRowViewModel>(
            ExecuteOpenClosureDetailAsync, row => row is not null && !IsBusy);
        _closeClosureDetailCommand = new AsyncRelayCommand(ExecuteCloseClosureDetailAsync, onError: HandleUnexpectedError);

        RegisterClosures = new ObservableCollection<RegisterClosureRowViewModel>();
        CashMovements = new ObservableCollection<CashMovementReportRowViewModel>();
        ProductSales = new ObservableCollection<ProductSalesRowViewModel>();
        LowStockItems = new ObservableCollection<InventoryCatalogRowViewModel>();
        OperatorActivity = new ObservableCollection<OperatorActivityRowViewModel>();
    }

    public ICommand LoadCommand => _loadCommand;

    public ICommand ClearFiltersCommand => _clearFiltersCommand;

    public ICommand OpenClosureDetailCommand => _openClosureDetailCommand;

    public ICommand CloseClosureDetailCommand => _closeClosureDetailCommand;

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetProperty(ref _selectedTabIndex, value);
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                TriggerImmediateReload();
            }
        }
    }

    public DateTime? ToDate
    {
        get => _toDate;
        set
        {
            if (SetProperty(ref _toDate, value))
            {
                TriggerImmediateReload();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                _openClosureDetailCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? DateRangeError
    {
        get => _dateRangeError;
        private set => SetProperty(ref _dateRangeError, value);
    }

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    // ---------- Ventas resumen (sección 9/10) ----------

    public int SummarySaleCount
    {
        get => _summarySaleCount;
        private set => SetProperty(ref _summarySaleCount, value);
    }

    public string SummaryGrossSalesText
    {
        get => _summaryGrossSalesText;
        private set => SetProperty(ref _summaryGrossSalesText, value);
    }

    public string SummaryCashSalesText
    {
        get => _summaryCashSalesText;
        private set => SetProperty(ref _summaryCashSalesText, value);
    }

    public string SummaryCardSalesText
    {
        get => _summaryCardSalesText;
        private set => SetProperty(ref _summaryCardSalesText, value);
    }

    public string? SalesSummaryStatusMessage
    {
        get => _salesSummaryStatusMessage;
        private set => SetProperty(ref _salesSummaryStatusMessage, value);
    }

    // ---------- Cierres de caja (RPT-CASH-01, sección 11/12) ----------

    public ObservableCollection<RegisterClosureRowViewModel> RegisterClosures { get; }

    public string? RegisterClosuresStatusMessage
    {
        get => _registerClosuresStatusMessage;
        private set => SetProperty(ref _registerClosuresStatusMessage, value);
    }

    public RegisterClosureRowViewModel? SelectedClosure
    {
        get => _selectedClosure;
        set => SetProperty(ref _selectedClosure, value);
    }

    public bool IsShowingClosureDetail => _isShowingClosureDetail && _selectedClosureDetail is not null;

    public bool IsShowingClosureList => !IsShowingClosureDetail;

    public RegisterClosureDetailViewModel? SelectedClosureDetail
    {
        get => _selectedClosureDetail;
        private set
        {
            if (SetProperty(ref _selectedClosureDetail, value))
            {
                OnPropertyChanged(nameof(IsShowingClosureDetail));
                OnPropertyChanged(nameof(IsShowingClosureList));
            }
        }
    }

    // ---------- Movimientos de caja (sección 13) ----------

    public ObservableCollection<CashMovementReportRowViewModel> CashMovements { get; }

    public string CashInTotalText
    {
        get => _cashInTotalText;
        private set => SetProperty(ref _cashInTotalText, value);
    }

    public string CashOutTotalText
    {
        get => _cashOutTotalText;
        private set => SetProperty(ref _cashOutTotalText, value);
    }

    public string? CashMovementsStatusMessage
    {
        get => _cashMovementsStatusMessage;
        private set => SetProperty(ref _cashMovementsStatusMessage, value);
    }

    // ---------- Ventas por producto (sección 14) ----------

    public ObservableCollection<ProductSalesRowViewModel> ProductSales { get; }

    public string? ProductSalesStatusMessage
    {
        get => _productSalesStatusMessage;
        private set => SetProperty(ref _productSalesStatusMessage, value);
    }

    // ---------- Stock bajo (sección 16) ----------

    public ObservableCollection<InventoryCatalogRowViewModel> LowStockItems { get; }

    public string? LowStockStatusMessage
    {
        get => _lowStockStatusMessage;
        private set => SetProperty(ref _lowStockStatusMessage, value);
    }

    // ---------- Actividad por operador (sección 17) ----------

    public ObservableCollection<OperatorActivityRowViewModel> OperatorActivity { get; }

    public string? OperatorActivityStatusMessage
    {
        get => _operatorActivityStatusMessage;
        private set => SetProperty(ref _operatorActivityStatusMessage, value);
    }

    // Llamado al navegar a Reportes (MainWindowViewModel, mismo patrón que InventoryViewModel/
    // ProductAuditViewModel.LoadCommand): recarga las 6 áreas con el rango vigente, sin cache
    // permanente.
    private Task ExecuteLoadAsync() => ReloadAllAsync();

    private void TriggerImmediateReload()
    {
        if (_loadCommand.CanExecute(null))
        {
            _loadCommand.Execute(null);
        }
    }

    private Task ExecuteClearFiltersAsync()
    {
        var today = _clock.UtcNow.ToLocalTime().Date;

        _fromDate = today;
        _toDate = today;

        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));

        return ReloadAllAsync();
    }

    private async Task ReloadAllAsync()
    {
        // Sección 23: From > To se rechaza en la UI antes de consultar, mismo criterio que
        // SalesHistoryViewModel.
        if (FromDate is { } fromDate && ToDate is { } toDate && fromDate.Date > toDate.Date)
        {
            DateRangeError = "La fecha inicial no puede ser posterior a la fecha final.";
            ClearAllPeriodData();
            return;
        }

        DateRangeError = null;
        IsBusy = true;

        try
        {
            var fromUtc = ToStartOfDayUtc(FromDate) ?? _clock.UtcNow;
            var toUtcExclusive = ToExclusiveEndOfDayUtc(ToDate) ?? _clock.UtcNow;

            var summaryTask = _reportsService.GetSalesSummaryAsync(fromUtc, toUtcExclusive);
            var closuresTask = _reportsService.GetRegisterClosuresAsync(fromUtc, toUtcExclusive);
            var cashMovementsTask = _reportsService.GetCashMovementsAsync(fromUtc, toUtcExclusive);
            var productSalesTask = _reportsService.GetProductSalesAsync(fromUtc, toUtcExclusive);
            var lowStockTask = _reportsService.GetLowStockAsync();
            var operatorActivityTask = _reportsService.GetOperatorActivityAsync(fromUtc, toUtcExclusive);

            var summary = await summaryTask;
            var closures = await closuresTask;
            var cashMovements = await cashMovementsTask;
            var productSales = await productSalesTask;
            var lowStock = await lowStockTask;
            var operatorActivity = await operatorActivityTask;

            ApplySalesSummary(summary);
            ApplyRegisterClosures(closures);
            ApplyCashMovements(cashMovements);
            ApplyProductSales(productSales);
            ApplyLowStock(lowStock);
            ApplyOperatorActivity(operatorActivity);

            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplySalesSummary(SalesSummaryReport summary)
    {
        SummarySaleCount = summary.SaleCount;
        SummaryGrossSalesText = FormatAmount(summary.GrossSales, summary.Currency);
        SummaryCashSalesText = FormatAmount(summary.CashSales, summary.Currency);
        SummaryCardSalesText = FormatAmount(summary.CardSales, summary.Currency);
        SalesSummaryStatusMessage = summary.SaleCount == 0
            ? "No hay ventas en el período seleccionado."
            : null;
    }

    private void ApplyRegisterClosures(IReadOnlyList<RegisterClosureReportItem> closures)
    {
        RegisterClosures.Clear();

        foreach (var item in closures)
        {
            RegisterClosures.Add(new RegisterClosureRowViewModel(item));
        }

        RegisterClosuresStatusMessage = closures.Count == 0
            ? "No hay cierres de caja en el período."
            : null;

        SelectedClosure = null;
        SetIsShowingClosureDetail(false);
        SelectedClosureDetail = null;
    }

    private void ApplyCashMovements(CashMovementsReportResult result)
    {
        CashMovements.Clear();

        foreach (var entry in result.Entries)
        {
            CashMovements.Add(new CashMovementReportRowViewModel(entry));
        }

        CashInTotalText = FormatAmount(result.CashInTotal, result.Currency);
        CashOutTotalText = FormatAmount(result.CashOutTotal, result.Currency);
        CashMovementsStatusMessage = result.Entries.Count == 0
            ? "No hay movimientos de caja."
            : null;
    }

    private void ApplyProductSales(IReadOnlyList<ProductSalesReportItem> items)
    {
        ProductSales.Clear();

        foreach (var item in items)
        {
            ProductSales.Add(new ProductSalesRowViewModel(item));
        }

        ProductSalesStatusMessage = items.Count == 0
            ? "No hay ventas de productos en el período seleccionado."
            : null;
    }

    private void ApplyLowStock(IReadOnlyList<Pos.Application.Inventory.InventoryCatalogItem> items)
    {
        LowStockItems.Clear();

        foreach (var item in items)
        {
            LowStockItems.Add(new InventoryCatalogRowViewModel(item));
        }

        LowStockStatusMessage = items.Count == 0
            ? "No hay productos con stock bajo o sin existencia."
            : null;
    }

    private void ApplyOperatorActivity(IReadOnlyList<OperatorActivityReportItem> items)
    {
        OperatorActivity.Clear();

        foreach (var item in items)
        {
            OperatorActivity.Add(new OperatorActivityRowViewModel(item));
        }

        OperatorActivityStatusMessage = items.Count == 0
            ? "No hay actividad de operadores en el período seleccionado."
            : null;
    }

    private void ClearAllPeriodData()
    {
        SummarySaleCount = 0;
        SummaryGrossSalesText = "0.00";
        SummaryCashSalesText = "0.00";
        SummaryCardSalesText = "0.00";
        SalesSummaryStatusMessage = null;

        RegisterClosures.Clear();
        RegisterClosuresStatusMessage = null;
        SelectedClosure = null;
        SetIsShowingClosureDetail(false);
        SelectedClosureDetail = null;

        CashMovements.Clear();
        CashInTotalText = "0.00";
        CashOutTotalText = "0.00";
        CashMovementsStatusMessage = null;

        ProductSales.Clear();
        ProductSalesStatusMessage = null;

        OperatorActivity.Clear();
        OperatorActivityStatusMessage = null;
    }

    private async Task ExecuteOpenClosureDetailAsync(RegisterClosureRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var detail = await _reportsService.GetRegisterClosureDetailAsync(row.Item.RegisterSessionId);

            if (detail is null)
            {
                GeneralError = "No fue posible cargar el detalle del cierre.";
                SelectedClosureDetail = null;
                SetIsShowingClosureDetail(false);

                return;
            }

            SelectedClosureDetail = new RegisterClosureDetailViewModel(detail);
            SetIsShowingClosureDetail(true);
            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteCloseClosureDetailAsync()
    {
        SetIsShowingClosureDetail(false);
        SelectedClosureDetail = null;

        return Task.CompletedTask;
    }

    private void SetIsShowingClosureDetail(bool value)
    {
        if (_isShowingClosureDetail == value)
        {
            return;
        }

        _isShowingClosureDetail = value;
        OnPropertyChanged(nameof(IsShowingClosureDetail));
        OnPropertyChanged(nameof(IsShowingClosureList));
    }

    private static string FormatAmount(decimal amount, string currency) =>
        string.IsNullOrEmpty(currency)
            ? amount.ToString("N2", CultureInfo.CurrentCulture)
            : $"{amount.ToString("N2", CultureInfo.CurrentCulture)} {currency}";

    private static DateTimeOffset? ToStartOfDayUtc(DateTime? localDate)
    {
        if (localDate is not { } value)
        {
            return null;
        }

        return new DateTimeOffset(value.Date, TimeZoneInfo.Local.GetUtcOffset(value.Date)).ToUniversalTime();
    }

    // Límite EXCLUSIVO (mismo criterio que SalesHistoryViewModel/InventoryViewModel): evita excluir
    // ventas del resto del día "Hasta".
    private static DateTimeOffset? ToExclusiveEndOfDayUtc(DateTime? localDate)
    {
        if (localDate is not { } value)
        {
            return null;
        }

        var nextDay = value.Date.AddDays(1);

        return new DateTimeOffset(nextDay, TimeZoneInfo.Local.GetUtcOffset(nextDay)).ToUniversalTime();
    }

    private void HandleUnexpectedError(Exception exception) => GeneralError = "Ocurrió un error inesperado.";
}
