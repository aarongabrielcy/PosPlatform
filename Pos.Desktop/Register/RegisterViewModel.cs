using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Pos.Application.Authentication;
using Pos.Application.CashMovements;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Common;
using Pos.Domain.Security;

namespace Pos.Desktop.Register;

// Resumen de caja (TAREA 24C, sección 21): reutiliza ICurrentRegisterSession, la misma fuente que
// alimenta el encabezado compacto del shell. Cerrar caja se resuelve igual que en
// MainWindowViewModel: el ViewModel nunca abre ventanas, solo pide cerrar.
// BASIC-CASH-01: agrega Entrada/Salida de efectivo (gateadas por Permission.ManageCashMovements) y
// el historial de movimientos de la sesión actual (gateado por Permission.ViewCashTotals, sección
// 17 de la tarea) — mismo patrón "solo pide, nunca abre ventanas" para los nuevos comandos.
public sealed partial class RegisterViewModel : ViewModelBase
{
    private readonly ICurrentRegisterSession _registerSession;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ICashMovementService _cashMovementService;
    private readonly ILogger<RegisterViewModel> _logger;
    private readonly AsyncRelayCommand _closeRegisterCommand;
    private readonly AsyncRelayCommand _cashInCommand;
    private readonly AsyncRelayCommand _cashOutCommand;
    private readonly AsyncRelayCommand _refreshMovementsCommand;

    private bool _isBusy;
    private string? _movementsError;

    public RegisterViewModel(
        ICurrentRegisterSession registerSession,
        ICurrentUserSession currentUserSession,
        ICashMovementService cashMovementService,
        ILogger<RegisterViewModel> logger)
    {
        _registerSession = registerSession ?? throw new ArgumentNullException(nameof(registerSession));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _cashMovementService = cashMovementService ?? throw new ArgumentNullException(nameof(cashMovementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _closeRegisterCommand = new AsyncRelayCommand(ExecuteCloseRegisterAsync);
        _cashInCommand = new AsyncRelayCommand(ExecuteCashInAsync, () => CanManageCashMovements && !IsBusy);
        _cashOutCommand = new AsyncRelayCommand(ExecuteCashOutAsync, () => CanManageCashMovements && !IsBusy);
        _refreshMovementsCommand = new AsyncRelayCommand(LoadMovementsAsync, () => !IsBusy);
    }

    // El shell (MainWindowViewModel) reenvía el cierre real, aplicando el mismo bloqueo por
    // carrito con líneas que ya existía en MainWindowViewModel.
    public event EventHandler? CloseRegisterRequested;

    // El código detrás de MainWindow reenvía hasta App.xaml.cs, único lugar que resuelve
    // RecordCashMovementWindow desde el contenedor de DI (mismo patrón que CloseRegisterRequested).
    public event EventHandler? CashInRequested;

    public event EventHandler? CashOutRequested;

    public ICommand CloseRegisterCommand => _closeRegisterCommand;

    public ICommand CashInCommand => _cashInCommand;

    public ICommand CashOutCommand => _cashOutCommand;

    public ICommand RefreshMovementsCommand => _refreshMovementsCommand;

    public bool IsRegisterOpen => _registerSession.IsOpen;

    public string RegisterName => _registerSession.Current?.RegisterName ?? string.Empty;

    public string OpenedByDisplayName => _registerSession.Current?.OpenedByDisplayName ?? string.Empty;

    public string OpenedAtText => _registerSession.Current is { } session
        ? session.OpenedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
        : string.Empty;

    public string OpeningAmountText => _registerSession.Current is { } session
        ? $"{session.OpeningAmount.ToString("N2", CultureInfo.CurrentCulture)} {session.Currency}"
        : string.Empty;

    public string StatusText => IsRegisterOpen ? "Caja abierta" : "Caja cerrada";

    // Sección 16 de la tarea: Owner/Admin/Manager pueden registrar CashIn/CashOut, Cashier no.
    public bool CanManageCashMovements => _currentUserSession.CurrentUser?.HasPermission(Permission.ManageCashMovements) ?? false;

    // Sección 17 de la tarea: el historial detallado de movimientos es información administrativa
    // (Manager/Admin), separada de lo que Cashier ya ve en el resumen de cierre.
    public bool CanViewCashMovements => _currentUserSession.CurrentUser?.HasPermission(Permission.ViewCashTotals) ?? false;

    public ObservableCollection<CashMovementRowViewModel> Movements { get; } = [];

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _cashInCommand.RaiseCanExecuteChanged();
                _cashOutCommand.RaiseCanExecuteChanged();
                _refreshMovementsCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? MovementsError
    {
        get => _movementsError;
        private set => SetProperty(ref _movementsError, value);
    }

    // Llamado al entrar a la sección Caja y, desde App.xaml.cs, tras registrar un movimiento con
    // éxito (sección 21: la lista debe reflejar el movimiento recién creado sin reabrir la vista).
    public async Task LoadMovementsAsync()
    {
        if (!CanViewCashMovements || !IsRegisterOpen)
        {
            Movements.Clear();
            return;
        }

        MovementsError = null;
        IsBusy = true;

        try
        {
            var result = await _cashMovementService.GetCurrentSessionMovementsAsync();

            Movements.Clear();

            if (result.Success)
            {
                foreach (var entry in result.Movements!)
                {
                    Movements.Add(new CashMovementRowViewModel(entry));
                }
            }
            else
            {
                MovementsError = "No se pudo cargar el historial de movimientos de caja.";
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogUnexpectedLoadMovementsError(_logger, exception);
            MovementsError = "No se pudo cargar el historial de movimientos de caja.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteCloseRegisterAsync()
    {
        CloseRegisterRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteCashInAsync()
    {
        CashInRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    private Task ExecuteCashOutAsync()
    {
        CashOutRequested?.Invoke(this, EventArgs.Empty);

        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al cargar los movimientos de caja.")]
    private static partial void LogUnexpectedLoadMovementsError(ILogger logger, Exception exception);
}
