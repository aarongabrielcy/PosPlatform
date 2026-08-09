using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Pos.Application.Common.Time;
using Pos.Application.Sales.History;
using Pos.Desktop.Common;
using Pos.Domain.Sales;

namespace Pos.Desktop.Sales.History;

// Ventas > Historial (TAREA 25B): módulo administrativo de solo lectura sobre ventas Completed.
// Dos estados internos (LISTA/DETALLE) dentro de la misma pantalla, nunca una Window nueva
// (sección 22). Read-only: no expone Edit/Delete/Cancel/Return/Refund (sección 50).
public sealed class SalesHistoryViewModel : ViewModelBase
{
    public const int PageSize = 50;

    private readonly ISalesHistoryService _salesHistoryService;
    private readonly IClock _clock;
    private readonly TimeSpan _searchDebounceDelay;

    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _clearFiltersCommand;
    private readonly AsyncRelayCommand _nextPageCommand;
    private readonly AsyncRelayCommand _previousPageCommand;
    private readonly AsyncRelayCommand<SalesHistoryRowViewModel> _openDetailCommand;
    private readonly AsyncRelayCommand _closeDetailCommand;

    private CancellationTokenSource? _searchCts;
    private bool _filterOptionsLoaded;

    private string _searchText = string.Empty;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private CashierFilterOption _selectedCashier = CashierFilterOption.All;
    private RegisterFilterOption _selectedRegister = RegisterFilterOption.All;
    private PaymentMethod? _selectedPaymentMethod;

    private bool _isBusy;
    private string? _generalError;
    private string? _dateRangeError;
    private string? _statusMessage;
    private int _currentPage = 1;
    private bool _canGoNext;

    private int _summaryCount;
    private string _summaryTotalText = string.Empty;
    private string _summaryBreakdownText = string.Empty;

    private bool _isShowingDetailRequested;
    private SaleHistoryDetailViewModel? _selectedSaleDetail;
    private SalesHistoryRowViewModel? _selectedItem;

    public SalesHistoryViewModel(
        ISalesHistoryService salesHistoryService, IClock clock, TimeSpan? searchDebounceDelay = null)
    {
        _salesHistoryService = salesHistoryService ?? throw new ArgumentNullException(nameof(salesHistoryService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _searchDebounceDelay = searchDebounceDelay ?? TimeSpan.FromMilliseconds(250);

        // Desde/Hasta = HOY (sección 11/45): operación diaria, evita cargar el historial completo.
        var today = _clock.UtcNow.ToLocalTime().Date;
        _fromDate = today;
        _toDate = today;

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, onError: HandleUnexpectedError);
        _searchCommand = new AsyncRelayCommand(() => RunImmediateSearchAsync(resetToFirstPage: true), onError: HandleUnexpectedError);
        _refreshCommand = new AsyncRelayCommand(() => RunImmediateSearchAsync(resetToFirstPage: false), onError: HandleUnexpectedError);
        _clearFiltersCommand = new AsyncRelayCommand(ExecuteClearFiltersAsync, onError: HandleUnexpectedError);
        _nextPageCommand = new AsyncRelayCommand(ExecuteNextPageAsync, () => CanGoNext && !IsBusy, HandleUnexpectedError);
        _previousPageCommand = new AsyncRelayCommand(ExecutePreviousPageAsync, () => CanGoPrevious && !IsBusy, HandleUnexpectedError);
        _openDetailCommand = new AsyncRelayCommand<SalesHistoryRowViewModel>(
            ExecuteOpenDetailAsync, row => row is not null && !IsBusy, HandleUnexpectedError);
        _closeDetailCommand = new AsyncRelayCommand(ExecuteCloseDetailAsync, onError: HandleUnexpectedError);

        Items = new ObservableCollection<SalesHistoryRowViewModel>();
        CashierOptions = new ObservableCollection<CashierFilterOption> { CashierFilterOption.All };
        RegisterOptions = new ObservableCollection<RegisterFilterOption> { RegisterFilterOption.All };
    }

    public ICommand LoadCommand => _loadCommand;

    public ICommand SearchCommand => _searchCommand;

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand ClearFiltersCommand => _clearFiltersCommand;

    public ICommand NextPageCommand => _nextPageCommand;

    public ICommand PreviousPageCommand => _previousPageCommand;

    public ICommand OpenDetailCommand => _openDetailCommand;

    public ICommand CloseDetailCommand => _closeDetailCommand;

    public ObservableCollection<SalesHistoryRowViewModel> Items { get; }

    public ObservableCollection<CashierFilterOption> CashierOptions { get; }

    public ObservableCollection<RegisterFilterOption> RegisterOptions { get; }

    // Expone la búsqueda con debounce en curso para pruebas deterministas, mismo patrón que
    // SalesViewModel/InventoryViewModel.PendingSearchTask (TAREA 24F, sección 27).
    internal Task PendingSearchTask { get; private set; } = Task.CompletedTask;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ScheduleDebouncedSearch();
            }
        }
    }

    public DateTime? FromDate
    {
        get => _fromDate;
        set
        {
            if (SetProperty(ref _fromDate, value))
            {
                TriggerImmediateSearch();
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
                TriggerImmediateSearch();
            }
        }
    }

    public CashierFilterOption SelectedCashier
    {
        get => _selectedCashier;
        set
        {
            if (SetProperty(ref _selectedCashier, value ?? CashierFilterOption.All))
            {
                TriggerImmediateSearch();
            }
        }
    }

    public RegisterFilterOption SelectedRegister
    {
        get => _selectedRegister;
        set
        {
            if (SetProperty(ref _selectedRegister, value ?? RegisterFilterOption.All))
            {
                TriggerImmediateSearch();
            }
        }
    }

    public PaymentMethod? SelectedPaymentMethod
    {
        get => _selectedPaymentMethod;
        set
        {
            if (SetProperty(ref _selectedPaymentMethod, value))
            {
                TriggerImmediateSearch();
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
                _nextPageCommand.RaiseCanExecuteChanged();
                _previousPageCommand.RaiseCanExecuteChanged();
                _openDetailCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string? GeneralError
    {
        get => _generalError;
        private set => SetProperty(ref _generalError, value);
    }

    public string? DateRangeError
    {
        get => _dateRangeError;
        private set => SetProperty(ref _dateRangeError, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetProperty(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoPrevious));
                _previousPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanGoPrevious => CurrentPage > 1;

    public bool CanGoNext
    {
        get => _canGoNext;
        private set
        {
            if (SetProperty(ref _canGoNext, value))
            {
                _nextPageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    // Resumen del FILTRO COMPLETO, no de la página cargada (sección 16/17).
    public int SummaryCount
    {
        get => _summaryCount;
        private set => SetProperty(ref _summaryCount, value);
    }

    public string SummaryTotalText
    {
        get => _summaryTotalText;
        private set => SetProperty(ref _summaryTotalText, value);
    }

    public string SummaryBreakdownText
    {
        get => _summaryBreakdownText;
        private set => SetProperty(ref _summaryBreakdownText, value);
    }

    public SalesHistoryRowViewModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    // Defensa contra Detail vacío (TAREA 25B-FIX, sección 4): IsShowingDetail nunca es true por sí
    // solo, siempre exige también SelectedSaleDetail != null. Evita que una futura inconsistencia
    // (p. ej. alguien pone _isShowingDetailRequested=true antes de tener el detail) renderice la
    // tarjeta "Detalle de venta" vacía.
    public bool IsShowingDetail => _isShowingDetailRequested && _selectedSaleDetail is not null;

    // Complemento de IsShowingDetail para bindear la Visibility de la lista sin necesitar un
    // converter booleano inverso (mismo criterio que IsBusy/IsNotBusy).
    public bool IsShowingList => !IsShowingDetail;

    public SaleHistoryDetailViewModel? SelectedSaleDetail
    {
        get => _selectedSaleDetail;
        private set
        {
            if (SetProperty(ref _selectedSaleDetail, value))
            {
                OnPropertyChanged(nameof(IsShowingDetail));
                OnPropertyChanged(nameof(IsShowingList));
            }
        }
    }

    // Cambia únicamente la intención de mostrar Detail; el valor efectivo de IsShowingDetail
    // siempre pasa por la condición combinada de arriba.
    private void SetIsShowingDetailRequested(bool value)
    {
        if (_isShowingDetailRequested == value)
        {
            return;
        }

        _isShowingDetailRequested = value;
        OnPropertyChanged(nameof(IsShowingDetail));
        OnPropertyChanged(nameof(IsShowingList));
    }

    // Llamado al navegar a Ventas > Historial (MainWindowViewModel, mismo patrón que
    // ProductAuditViewModel/InventoryViewModel.LoadCommand): la primera vez carga las opciones de
    // filtro (Cajero/Caja); siempre recarga la página actual con los filtros vigentes, nunca
    // reinicia Desde/Hasta a "hoy" en navegaciones subsecuentes (sección 33/45 — ese reinicio solo
    // ocurre una vez, al construir el ViewModel, y explícitamente vía ClearFiltersCommand).
    private async Task ExecuteLoadAsync()
    {
        if (!_filterOptionsLoaded)
        {
            await LoadFilterOptionsAsync();
            _filterOptionsLoaded = true;
        }

        await RunImmediateSearchAsync(resetToFirstPage: true);
    }

    private async Task LoadFilterOptionsAsync()
    {
        var options = await _salesHistoryService.GetFilterOptionsAsync();

        CashierOptions.Clear();
        CashierOptions.Add(CashierFilterOption.All);

        foreach (var cashier in options.Cashiers)
        {
            CashierOptions.Add(new CashierFilterOption(cashier.UserId, cashier.DisplayName));
        }

        RegisterOptions.Clear();
        RegisterOptions.Add(RegisterFilterOption.All);

        foreach (var register in options.Registers)
        {
            RegisterOptions.Add(new RegisterFilterOption(register.RegisterId, register.Name));
        }
    }

    // Cambiar Desde/Hasta/Cajero/Caja/Método busca de inmediato (sin debounce, TAREA 24G sección
    // 12 mismo criterio que SelectedStockFilter): solo el texto libre usa debounce.
    private void TriggerImmediateSearch()
    {
        if (_searchCommand.CanExecute(null))
        {
            _searchCommand.Execute(null);
        }
    }

    private void ScheduleDebouncedSearch()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        PendingSearchTask = RunDebouncedSearchAsync(cts.Token);
    }

    private async Task RunDebouncedSearchAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_searchDebounceDelay, cancellationToken);
            await LoadPageAsync(resetToFirstPage: true, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Un nuevo carácter escrito canceló este debounce: comportamiento esperado, no un error.
        }
    }

    // Comparte el mismo _searchCts que el debounce de texto (sección 15): un filtro/página
    // inmediata cancela cualquier debounce pendiente, y viceversa, así una respuesta tardía nunca
    // sobrescribe un resultado más nuevo sin importar qué camino la disparó.
    private async Task RunImmediateSearchAsync(bool resetToFirstPage)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        var cts = new CancellationTokenSource();
        _searchCts = cts;
        var task = LoadPageAsync(resetToFirstPage, cts.Token);
        PendingSearchTask = task;

        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Una carga más reciente reemplazó a esta: no es un error que deba mostrarse.
        }
    }

    private async Task LoadPageAsync(bool resetToFirstPage, CancellationToken cancellationToken = default)
    {
        if (resetToFirstPage)
        {
            CurrentPage = 1;
        }

        // Validación de rango (sección 47): si Desde > Hasta no se consulta, se muestra el error
        // inline (nunca MessageBox) y se limpia la grilla/resumen.
        if (FromDate is { } fromDate && ToDate is { } toDate && fromDate.Date > toDate.Date)
        {
            DateRangeError = "La fecha inicial no puede ser posterior a la fecha final.";
            Items.Clear();
            SummaryCount = 0;
            SummaryTotalText = string.Empty;
            SummaryBreakdownText = string.Empty;
            CanGoNext = false;
            StatusMessage = null;

            return;
        }

        DateRangeError = null;
        IsBusy = true;

        try
        {
            var filter = BuildFilter();
            var skip = (CurrentPage - 1) * PageSize;

            var pageTask = _salesHistoryService.SearchPageAsync(filter, skip, PageSize, cancellationToken);
            var summaryTask = _salesHistoryService.GetSummaryAsync(filter, cancellationToken);

            var page = await pageTask;
            var summary = await summaryTask;

            // Revalidado después de esperar porque los fakes/servicios no siempre observan el
            // CancellationToken (TAREA 24F, sección 4): una respuesta tardía de un filtro viejo
            // nunca reemplaza los resultados de una búsqueda más nueva.
            cancellationToken.ThrowIfCancellationRequested();

            Items.Clear();

            foreach (var item in page.Items)
            {
                Items.Add(new SalesHistoryRowViewModel(item));
            }

            CanGoNext = page.HasNextPage;

            SummaryCount = summary.SalesCount;
            SummaryTotalText = summary.SalesCount == 0
                ? "0.00"
                : $"{summary.Total.ToString("N2", CultureInfo.CurrentCulture)} {summary.Currency}";
            SummaryBreakdownText = summary.PaymentBreakdown.Count == 0
                ? "-"
                : string.Join(", ", summary.PaymentBreakdown.Select(payment =>
                    $"{SalesHistoryDisplayFormatter.ToMethodLabel(payment.Method)}: {payment.Amount.ToString("N2", CultureInfo.CurrentCulture)}"));

            // Estado vacío explícito (sección 31), nunca un error.
            StatusMessage = Items.Count switch
            {
                0 => "No se encontraron ventas para los filtros seleccionados.",
                1 => "1 venta mostrada.",
                _ => $"{Items.Count} ventas mostradas.",
            };

            GeneralError = null;
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsBusy = false;
            }
        }
    }

    private SalesHistoryFilter BuildFilter() => new(
        FromUtc: ToStartOfDayUtc(FromDate),
        ToUtc: ToExclusiveEndOfDayUtc(ToDate),
        CashierUserId: SelectedCashier.UserId,
        RegisterId: SelectedRegister.RegisterId,
        PaymentMethod: SelectedPaymentMethod,
        SearchTerm: string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim());

    private Task ExecuteNextPageAsync()
    {
        CurrentPage++;
        return RunImmediateSearchAsync(resetToFirstPage: false);
    }

    private Task ExecutePreviousPageAsync()
    {
        if (CurrentPage <= 1)
        {
            return Task.CompletedTask;
        }

        CurrentPage--;
        return RunImmediateSearchAsync(resetToFirstPage: false);
    }

    // "Limpiar filtros" restaura Desde/Hasta a HOY, no los deja vacíos (sección 46): el punto de
    // partida por defecto de Historial siempre es el día actual.
    private Task ExecuteClearFiltersAsync()
    {
        var today = _clock.UtcNow.ToLocalTime().Date;

        _searchText = string.Empty;
        _fromDate = today;
        _toDate = today;
        _selectedCashier = CashierFilterOption.All;
        _selectedRegister = RegisterFilterOption.All;
        _selectedPaymentMethod = null;

        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(FromDate));
        OnPropertyChanged(nameof(ToDate));
        OnPropertyChanged(nameof(SelectedCashier));
        OnPropertyChanged(nameof(SelectedRegister));
        OnPropertyChanged(nameof(SelectedPaymentMethod));

        return RunImmediateSearchAsync(resetToFirstPage: true);
    }

    // "Ver detalle" carga la venta EXACTA por SaleId (sección 32): nunca una heurística como "la
    // última venta del cajero". Null (no encontrada / de otra Organization / sin permiso) muestra
    // un error y permanece en la lista.
    private async Task ExecuteOpenDetailAsync(SalesHistoryRowViewModel? row)
    {
        if (row is null)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var detail = await _salesHistoryService.GetDetailAsync(row.Item.SaleId);

            if (detail is null)
            {
                GeneralError = "No fue posible cargar el detalle de la venta.";
                SelectedSaleDetail = null;
                SetIsShowingDetailRequested(false);

                return;
            }

            SelectedSaleDetail = new SaleHistoryDetailViewModel(detail);
            SetIsShowingDetailRequested(true);
            GeneralError = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    // "Volver al historial": conserva fechas/filtros/búsqueda/página tal como quedaron (sección
    // 22/36), solo cambia el estado visual LISTA/DETALLE. Nunca vuelve a consultar la base de datos.
    private Task ExecuteCloseDetailAsync()
    {
        SetIsShowingDetailRequested(false);
        SelectedSaleDetail = null;

        return Task.CompletedTask;
    }

    private static DateTimeOffset? ToStartOfDayUtc(DateTime? localDate)
    {
        if (localDate is not { } value)
        {
            return null;
        }

        return new DateTimeOffset(value.Date, TimeZoneInfo.Local.GetUtcOffset(value.Date)).ToUniversalTime();
    }

    // Límite EXCLUSIVO (sección 12): [fromInclusive, toExclusive) evita el error típico de excluir
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
