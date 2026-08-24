using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Pos.Application.Activation;
using Pos.Application.Authentication;
using Pos.Application.Common.Versioning;
using Pos.Application.Configuration;
using Pos.Application.Organizations;
using Pos.Application.Receipts;
using Pos.Desktop.Common;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop.LocalConfiguration;

// BASIC-CFG-01: pantalla "Configuración" (ManageSettings, Administrador únicamente - ver
// MainWindowViewModel.BuildNavigationItems). Un único ViewModel con 4 secciones (General/Negocio,
// Impresora, Cajón, Sistema - sección 29 de la tarea), igual criterio que ReportsViewModel/
// InventoryViewModel: varias áreas relacionadas en un solo ViewModel con pestañas, no varias
// pantallas de navegación separadas.
//
// El estado de Impresora se inicializa desde IReceiptPrinterOptionsProvider.Current (sección 35/36:
// "Configuration displays effective defaults"), NUNCA desde un LocalSettings vacío: Current ya
// resuelve la precedencia completa (appsettings -> appsettings.Local.json -> LocalAppData), así que
// mostrar cualquier otra cosa aquí sería inconsistente con lo que la app realmente imprime.
public sealed class LocalConfigurationViewModel : ViewModelBase
{
    private readonly ILocalSettingsStore _localSettingsStore;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IReceiptPrinterOptionsProvider _printerOptionsProvider;
    private readonly IPrinterDiscovery _printerDiscovery;
    private readonly IReceiptPrintingService _printingService;
    private readonly ICurrentUserSession _currentUserSession;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IApplicationVersionProvider _versionProvider;
    private readonly IApplicationPathProvider _pathProvider;
    private readonly IInstallationActivationStateService _activationStateService;

    private readonly AsyncRelayCommand _loadCommand;
    private readonly AsyncRelayCommand _refreshPrintersCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _testPrintCommand;

    private bool _isBusy;
    private string? _statusMessage;
    private string? _errorMessage;

    private bool _printerEnabled;
    private string? _selectedPrinterName;
    private ReceiptPaperWidth _selectedPaperWidth = ReceiptPaperWidth.Mm80;
    private bool _autoPrint = true;
    private bool _cutPaper;
    private string? _printerAvailabilityWarning;

    private bool _cashDrawerEnabled;
    private bool _openOnRegisterOpen;

    private string _organizationName = string.Empty;

    public LocalConfigurationViewModel(
        ILocalSettingsStore localSettingsStore,
        ILocalSettingsService localSettingsService,
        IReceiptPrinterOptionsProvider printerOptionsProvider,
        IPrinterDiscovery printerDiscovery,
        IReceiptPrintingService printingService,
        ICurrentUserSession currentUserSession,
        IOrganizationRepository organizationRepository,
        IApplicationVersionProvider versionProvider,
        IApplicationPathProvider pathProvider,
        IInstallationActivationStateService activationStateService)
    {
        _localSettingsStore = localSettingsStore ?? throw new ArgumentNullException(nameof(localSettingsStore));
        _localSettingsService = localSettingsService ?? throw new ArgumentNullException(nameof(localSettingsService));
        _printerOptionsProvider = printerOptionsProvider ?? throw new ArgumentNullException(nameof(printerOptionsProvider));
        _printerDiscovery = printerDiscovery ?? throw new ArgumentNullException(nameof(printerDiscovery));
        _printingService = printingService ?? throw new ArgumentNullException(nameof(printingService));
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _pathProvider = pathProvider ?? throw new ArgumentNullException(nameof(pathProvider));
        _activationStateService = activationStateService ?? throw new ArgumentNullException(nameof(activationStateService));

        _loadCommand = new AsyncRelayCommand(ExecuteLoadAsync, onError: HandleUnexpectedError);
        _refreshPrintersCommand = new AsyncRelayCommand(ExecuteRefreshPrintersAsync, onError: HandleUnexpectedError);
        _saveCommand = new AsyncRelayCommand(ExecuteSaveAsync, onError: HandleUnexpectedError);
        _testPrintCommand = new AsyncRelayCommand(ExecuteTestPrintAsync, onError: HandleUnexpectedError);

        AvailablePrinters = new ObservableCollection<string>();
        PaperWidthOptions =
        [
            new PaperWidthOption(ReceiptPaperWidth.Mm58, "58 mm"),
            new PaperWidthOption(ReceiptPaperWidth.Mm80, "80 mm"),
        ];

        ApplicationVersion = _versionProvider.GetVersion();
        SettingsFilePath = Path.Combine(_pathProvider.DataDirectory, "Config", "settings.json");
        LogsDirectoryPath = _pathProvider.LogsDirectory;
    }

    public ICommand LoadCommand => _loadCommand;

    public ICommand RefreshPrintersCommand => _refreshPrintersCommand;

    public ICommand SaveCommand => _saveCommand;

    public ICommand TestPrintCommand => _testPrintCommand;

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    // ---------- Impresora ----------

    public ObservableCollection<string> AvailablePrinters { get; }

    public IReadOnlyList<PaperWidthOption> PaperWidthOptions { get; }

    public bool PrinterEnabled
    {
        get => _printerEnabled;
        set => SetProperty(ref _printerEnabled, value);
    }

    public string? SelectedPrinterName
    {
        get => _selectedPrinterName;
        set
        {
            if (SetProperty(ref _selectedPrinterName, value))
            {
                RefreshPrinterAvailabilityWarning();
            }
        }
    }

    public ReceiptPaperWidth SelectedPaperWidth
    {
        get => _selectedPaperWidth;
        set => SetProperty(ref _selectedPaperWidth, value);
    }

    public bool AutoPrint
    {
        get => _autoPrint;
        set => SetProperty(ref _autoPrint, value);
    }

    public bool CutPaper
    {
        get => _cutPaper;
        set => SetProperty(ref _cutPaper, value);
    }

    // Sección 37 de la tarea: la impresora configurada ya no está entre las instaladas. Nunca
    // selecciona otra impresora en su lugar; solo informa.
    public string? PrinterAvailabilityWarning
    {
        get => _printerAvailabilityWarning;
        private set => SetProperty(ref _printerAvailabilityWarning, value);
    }

    // ---------- Cajón (fundación, sección 17/38 - sin apertura física) ----------

    public bool CashDrawerEnabled
    {
        get => _cashDrawerEnabled;
        set => SetProperty(ref _cashDrawerEnabled, value);
    }

    public bool OpenOnRegisterOpen
    {
        get => _openOnRegisterOpen;
        set => SetProperty(ref _openOnRegisterOpen, value);
    }

    // Propiedades auto-implementadas (no expresiones estáticas): WPF Binding resuelve propiedades
    // por reflexión de INSTANCIA (TypeDescriptor.GetProperties(item)), así que un miembro `static`
    // simplemente no se bindea - CA1822 sugeriría `static` aquí, pero seguirlo rompería el binding
    // de estas dos etiquetas en LocalConfigurationView.xaml.
    public string CashDrawerConnectionModeLabel { get; } = "Mediante impresora";

    public string CashDrawerFoundationNotice { get; } =
        "La apertura física del cajón aún no está implementada ni validada con el hardware de referencia. " +
        "Esta sección solo guarda la intención de configuración para cuando esa integración exista.";

    // ---------- Negocio (solo lectura, sección 21/22) ----------

    public string OrganizationName
    {
        get => _organizationName;
        private set => SetProperty(ref _organizationName, value);
    }

    // ---------- Sistema (solo lectura, sección 24) ----------

    public string ApplicationVersion { get; }

    public string SettingsFilePath { get; }

    public string LogsDirectoryPath { get; }

    public string ActivationStatusText { get; private set; } = string.Empty;

    private async Task ExecuteLoadAsync()
    {
        IsBusy = true;
        ErrorMessage = null;

        try
        {
            ApplyReceiptPrinterOptions(_printerOptionsProvider.Current);

            var savedSettings = await _localSettingsStore.LoadAsync();
            var cashDrawer = savedSettings?.CashDrawer ?? new LocalCashDrawerSettings();
            CashDrawerEnabled = cashDrawer.Enabled;
            OpenOnRegisterOpen = cashDrawer.OpenOnRegisterOpen;

            await LoadInstalledPrintersAsync();

            var organizationId = _currentUserSession.CurrentUser?.OrganizationId;
            if (organizationId is { } id)
            {
                var organization = await _organizationRepository.GetByIdAsync(id, CancellationToken.None);
                OrganizationName = organization?.Name ?? string.Empty;
            }

            var activationStatus = await _activationStateService.GetActivationStatusAsync(CancellationToken.None);
            ActivationStatusText = activationStatus switch
            {
                ActivationStatus.Activated => "Activada",
                ActivationStatus.NotActivated => "No activada",
                _ => activationStatus.ToString(),
            };
            OnPropertyChanged(nameof(ActivationStatusText));

            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private Task ExecuteRefreshPrintersAsync() => LoadInstalledPrintersAsync();

    private async Task LoadInstalledPrintersAsync()
    {
        var names = await _printerDiscovery.GetInstalledPrinterNamesAsync();

        AvailablePrinters.Clear();

        foreach (var name in names)
        {
            AvailablePrinters.Add(name);
        }

        RefreshPrinterAvailabilityWarning();
    }

    private void RefreshPrinterAvailabilityWarning()
    {
        PrinterAvailabilityWarning = !string.IsNullOrWhiteSpace(SelectedPrinterName) && !AvailablePrinters.Contains(SelectedPrinterName)
            ? "La impresora configurada no está entre las impresoras instaladas de Windows."
            : null;
    }

    private async Task ExecuteSaveAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var settings = new LocalSettings
            {
                ReceiptPrinter = new ReceiptPrinterOptions
                {
                    Enabled = PrinterEnabled,
                    PrinterName = SelectedPrinterName,
                    PaperWidth = SelectedPaperWidth,
                    AutoPrint = AutoPrint,
                    CutPaper = CutPaper,
                },
                CashDrawer = new LocalCashDrawerSettings
                {
                    Enabled = CashDrawerEnabled,
                    OpenOnRegisterOpen = OpenOnRegisterOpen,
                    ConnectionMode = CashDrawerConnectionMode.ViaReceiptPrinter,
                },
            };

            var result = await _localSettingsService.SaveAsync(settings);

            switch (result)
            {
                case LocalSettingsSaveResult.Saved:
                    StatusMessage = "Configuración guardada.";
                    ApplyReceiptPrinterOptions(_printerOptionsProvider.Current);
                    RefreshPrinterAvailabilityWarning();
                    break;

                case LocalSettingsSaveResult.NotAuthorized:
                    ErrorMessage = "No tienes permiso para guardar la configuración.";
                    break;

                default:
                    ErrorMessage = "No fue posible guardar la configuración local.";
                    break;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ExecuteTestPrintAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var result = await _printingService.PrintTestAsync();

            StatusMessage = result.Status switch
            {
                ReceiptPrintResultStatus.Success => "Ticket de prueba enviado a la impresora.",
                ReceiptPrintResultStatus.Skipped =>
                    "La impresión está deshabilitada o no hay una impresora configurada.",
                _ => null,
            };

            ErrorMessage = result.Status switch
            {
                ReceiptPrintResultStatus.NotAuthorized => "No tienes permiso para imprimir una prueba.",
                ReceiptPrintResultStatus.PrinterUnavailable or ReceiptPrintResultStatus.PrintFailed =>
                    "No fue posible imprimir la prueba. Verifica que la impresora esté encendida y conectada.",
                _ => null,
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyReceiptPrinterOptions(ReceiptPrinterOptions options)
    {
        PrinterEnabled = options.Enabled;
        SelectedPrinterName = string.IsNullOrWhiteSpace(options.PrinterName) ? null : options.PrinterName;
        SelectedPaperWidth = options.PaperWidth;
        AutoPrint = options.AutoPrint;
        CutPaper = options.CutPaper;
    }

    private void HandleUnexpectedError(Exception exception) => ErrorMessage = "Ocurrió un error inesperado.";
}
