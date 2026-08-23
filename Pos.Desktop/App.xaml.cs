using System.Globalization;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pos.Application.Activation;
using Pos.Application.Authentication;
using Pos.Application.Configuration;
using Pos.Application.Enforcement;
using Pos.Application.Inventory;
using Pos.Application.Installation;
using Pos.Application.Receipts;
using Pos.Application.Products.ManageProduct;
using Pos.Application.RegisterSessions;
using Pos.Application.Sales.Checkout;
using Pos.Application.SalesCart;
using Pos.Application.Security;
using Pos.Desktop.Activation;
using Pos.Desktop.CashMovements;
using Pos.Desktop.Common;
using Pos.Desktop.Configuration;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Enforcement;
using Pos.Desktop.InstallationHealth;
using Pos.Desktop.Inventory;
using Pos.Desktop.LocalConfiguration;
using Pos.Desktop.Login;
using Pos.Desktop.Main;
using Pos.Desktop.Products;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Products.Images;
using Pos.Desktop.Register;
using Pos.Desktop.RegisterSessions;
using Pos.Desktop.Reports;
using Pos.Desktop.Sales;
using Pos.Desktop.Sales.Checkout;
using Pos.Desktop.Sales.History;
using Pos.Desktop.Setup;
using Pos.Desktop.Users;
using Pos.Domain.CashMovements;
using Pos.Domain.Common.Identifiers;
using Pos.Hardware.EscPos;
using Pos.Hardware.Printing;
using Pos.Infrastructure;
using Pos.Infrastructure.Persistence.Initialization;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private const int InvalidInstallationStateExitCode = -3;
        private const int InvalidRegisterSessionStateExitCode = -4;

        private IHost? _host;
        private IServiceScope? _mainWindowScope;

        [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo al iniciar la aplicación.")]
        private static partial void LogStartupFailure(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo al inicializar la base de datos local.")]
        private static partial void LogDatabaseInitializationFailure(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "La instalación local presenta un estado inconsistente ({InstallationState}).")]
        private static partial void LogInvalidInstallationState(ILogger logger, InstallationState installationState);

        [LoggerMessage(Level = LogLevel.Critical, Message = "El estado de la caja presenta datos inconsistentes.")]
        private static partial void LogInvalidRegisterSessionState(ILogger logger);

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Evita que WPF cierre la aplicación por ShutdownMode.OnLastWindowClose (el valor
            // por defecto) cuando InitialSetupWindow o LoginWindow —únicas ventanas abiertas
            // durante el arranque— se cierran antes de que MainWindow llegue a mostrarse. El
            // flujo pasa a OnMainWindowClose recién en ShowMainWindow(), una vez que MainWindow
            // existe.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                _host = HostConfigurationFactory.CreateBaseBuilder(AppContext.BaseDirectory)
                    .UseDefaultServiceProvider(options =>
                    {
                        options.ValidateScopes = true;
                        options.ValidateOnBuild = true;
                    })
                    .ConfigureServices((context, services) =>
                    {
                        var activationBaseUrl = context.Configuration["Activation:BaseUrl"];

                        if (string.IsNullOrWhiteSpace(activationBaseUrl))
                        {
                            throw new InvalidOperationException(
                                "La configuración 'Activation:BaseUrl' es obligatoria (ver appsettings.json).");
                        }

                        services.AddPosInfrastructure(activationBaseUrl);
                        services.AddHostedService<InstallationHeartbeatBackgroundService>();

                        // BASIC-UX-01: IProductImageStore decodifica/normaliza con las APIs de
                        // imaging de WPF, exclusivas de Pos.Desktop (igual motivo que
                        // IReceiptFormatter/IReceiptPrinter más abajo). IProductPhotoService (ver
                        // IProductPhotoService) se registra aquí, no en AddPosInfrastructure, por la
                        // misma razón que IReceiptPrintingService: depende de IProductImageStore,
                        // exclusivo de Pos.Desktop. IDesktopClock envuelve la hora local del sistema
                        // para el header, separado de IClock (UTC, Application).
                        services.AddSingleton<IProductImageStore, WpfProductImageStore>();
                        services.AddScoped<IProductPhotoService, ProductPhotoService>();
                        services.AddSingleton<IDesktopClock, SystemDesktopClock>();

                        // BASIC-PRN-01: opciones leídas una sola vez al arrancar (appsettings.json +
                        // appsettings.Local.json, ver ReceiptPrinterOptionsFactory). El formateador
                        // ESC/POS y el transporte de spooler son singletons de Pos.Hardware; Desktop es
                        // el único proyecto con permiso de referenciarlo y con Microsoft.Extensions.
                        // DependencyInjection disponible (Pos.Hardware no puede tener PackageReference).
                        // BASIC-CFG-01: los valores leídos de appsettings.json/appsettings.Local.json
                        // pasan a ser solo el nivel más bajo de precedencia (sección 35 de la tarea).
                        // IReceiptPrinterOptionsProvider.Current se recalcula en RefreshAsync (llamado
                        // más abajo tras _host.Start(), y de nuevo tras cada Guardar exitoso en
                        // Configuración > Impresora) combinándolos con ILocalSettingsStore.
                        var receiptPrinterOptions = ReceiptPrinterOptionsFactory.Create(context.Configuration);
                        services.AddSingleton<IReceiptPrinterOptionsProvider>(sp =>
                            new ReceiptPrinterOptionsProvider(receiptPrinterOptions, sp.GetRequiredService<ILocalSettingsStore>()));
                        services.AddSingleton<IReceiptFormatter, EscPosReceiptFormatter>();
                        services.AddSingleton<IReceiptPrinter, WindowsSpoolReceiptPrinter>();
                        services.AddSingleton<IPrinterDiscovery, WindowsPrinterDiscovery>();

                        // IReceiptPrintingService se registra aquí (no en AddPosInfrastructure) porque
                        // depende de IReceiptFormatter/IReceiptPrinter, exclusivos de Pos.Hardware/
                        // Pos.Desktop: AddPosInfrastructureBuildsContainerWithScopeAndBuildValidationEnabled
                        // (Pos.Infrastructure.Tests) exige que el contenedor de AddPosInfrastructure sea
                        // válido por sí solo, sin depender de registros que solo aporta este proyecto.
                        services.AddScoped<IReceiptPrintingService, ReceiptPrintingService>();

                        // BASIC-CFG-01: mismo motivo que IReceiptPrintingService arriba - depende de
                        // IReceiptPrinterOptionsProvider, registrado solo aquí (necesita IConfiguration,
                        // exclusivo de Pos.Desktop).
                        services.AddScoped<ILocalSettingsService, LocalSettingsService>();
                        services.AddTransient<ActivationViewModel>();
                        services.AddTransient<ActivationWindow>();
                        services.AddTransient<SuspensionViewModel>();
                        services.AddTransient<SuspensionWindow>();
                        services.AddTransient<CredentialRecoveryViewModel>();
                        services.AddTransient<CredentialRecoveryWindow>();
                        services.AddTransient<NewInstallationActivationViewModel>();
                        services.AddTransient<NewInstallationActivationWindow>();
                        services.AddTransient<MainWindow>();
                        services.AddTransient<MainWindowViewModel>();
                        services.AddTransient<DashboardViewModel>();
                        services.AddTransient<SalesViewModel>();
                        services.AddTransient<SalesHistoryViewModel>();
                        services.AddTransient<ProductsViewModel>();
                        services.AddTransient<InventoryViewModel>();
                        services.AddTransient<RegisterViewModel>();
                        services.AddTransient<UserManagementViewModel>();
                        services.AddTransient<CreateUserViewModel>();
                        services.AddTransient<CreateUserWindow>();
                        services.AddTransient<EditUserViewModel>();
                        services.AddTransient<EditUserWindow>();
                        services.AddTransient<ProductAuditViewModel>();
                        services.AddTransient<ReportsViewModel>();
                        services.AddTransient<LocalConfigurationViewModel>();
                        services.AddTransient<NotificationCenterViewModel>();
                        services.AddTransient<InitialSetupViewModel>();
                        services.AddTransient<InitialSetupWindow>();
                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();
                        services.AddTransient<OpenRegisterSessionViewModel>();
                        services.AddTransient<OpenRegisterSessionWindow>();
                        services.AddTransient<CloseRegisterSessionViewModel>();
                        services.AddTransient<CloseRegisterSessionWindow>();
                        services.AddTransient<CreateProductViewModel>();
                        services.AddTransient<CreateProductWindow>();
                        services.AddTransient<EditProductViewModel>();
                        services.AddTransient<EditProductWindow>();
                        services.AddTransient<AdjustInventoryViewModel>();
                        services.AddTransient<AdjustInventoryWindow>();
                        services.AddTransient<CheckoutViewModel>();
                        services.AddTransient<CheckoutWindow>();
                        services.AddTransient<RecordCashMovementViewModel>();
                        services.AddTransient<RecordCashMovementWindow>();
                    })
                    .Build();

                // InstallationHeartbeatBackgroundService es el único IHostedService registrado.
                // BackgroundService.StartAsync() no espera a que ExecuteAsync termine, solo a que
                // arranque, así que Start() sigue sin bloquear el hilo de UI de forma perceptible
                // (ver Pos.Desktop.InstallationHealth.InstallationHeartbeatBackgroundService).
                _host.Start();

                var pathProvider = _host.Services.GetRequiredService<IApplicationPathProvider>();
                pathProvider.EnsureDataDirectoryExists();

                // BASIC-CFG-01, sección 35/36: carga la configuración de impresora guardada en
                // LocalAppData (si existe y es legible) antes de que cualquier venta pueda
                // imprimir. Ausencia de archivo o archivo corrupto deja Current en los valores de
                // appsettings/appsettings.Local.json (ver ReceiptPrinterOptionsProvider), nunca
                // bloquea el arranque.
                var receiptPrinterOptionsProvider = _host.Services.GetRequiredService<IReceiptPrinterOptionsProvider>();
                await receiptPrinterOptionsProvider.RefreshAsync(CancellationToken.None);

                _mainWindowScope = _host.Services.CreateScope();

                var initializer = _mainWindowScope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>();
                await initializer.InitializeAsync();

                var activationStateService = _mainWindowScope.ServiceProvider.GetRequiredService<IInstallationActivationStateService>();
                var activationStatus = await activationStateService.GetActivationStatusAsync(CancellationToken.None);
                var activationDecision = StartupFlowCoordinator.DecideForActivationStatus(activationStatus);

                if (activationDecision == StartupFlowDecision.ShowActivationDialog)
                {
                    var activationWindow = _mainWindowScope.ServiceProvider.GetRequiredService<ActivationWindow>();
                    var activationCompleted = activationWindow.ShowDialog();

                    if (StartupFlowCoordinator.DecideForActivationDialogResult(activationCompleted) == StartupFlowDecision.ShutdownCancelled)
                    {
                        Shutdown(0);
                        return;
                    }
                }

                // Puerta de enforcement (sección 21 de la tarea): posterior a la activación y
                // previa a la configuración/login del negocio local. InitializeAsync carga el
                // estado persistido exactamente una vez, antes de que StartupFlowCoordinator lo lea.
                var enforcementStateService = _mainWindowScope.ServiceProvider.GetRequiredService<IInstallationEnforcementStateService>();
                await enforcementStateService.InitializeAsync(CancellationToken.None);

                var enforcementDecision = StartupFlowCoordinator.DecideForEnforcementState(enforcementStateService.Current);

                if (enforcementDecision != StartupFlowDecision.ContinueAfterEnforcementCheck)
                {
                    var restrictedFlowDialogResult = ShowRestrictedFlowDialog(enforcementDecision);

                    if (StartupFlowCoordinator.DecideForRestrictedFlowDialogResult(restrictedFlowDialogResult) == StartupFlowDecision.ShutdownCancelled)
                    {
                        Shutdown(0);
                        return;
                    }
                }

                // READ-ONLY CORRECTION: reconcilia los permisos de los Roles canónicos (Administrator/
                // Manager/Cashier) ANTES de evaluar el estado estructural de la instalación. Necesario
                // porque InstallationStructureInspector (usado por GetInstallationStateAsync) exige que
                // el Role administrativo YA PERSISTIDO contenga TODOS los valores actuales de
                // Permission: sin esta reconciliación aquí, una instalación existente cuyo Administrator
                // se sembró antes de agregar un Permission nuevo dejaría de detectarse como
                // administrativa y el arranque fallaría con InvalidState. Es la misma llamada
                // idempotente que se repite más abajo tras Setup (para crear Manager/Cashier en una
                // instalación recién configurada); repetirla aquí no tiene efecto donde ya no hay nada
                // que reconciliar.
                var earlyRoleSeedingService = _mainWindowScope.ServiceProvider.GetRequiredService<IStandardRoleSeedingService>();
                await earlyRoleSeedingService.EnsureStandardRolesExistAsync(CancellationToken.None);

                var installationStateService = _mainWindowScope.ServiceProvider.GetRequiredService<IInstallationStateService>();
                var installationState = await installationStateService.GetInstallationStateAsync(CancellationToken.None);
                var initialDecision = StartupFlowCoordinator.DecideForInstallationState(installationState);

                if (initialDecision == StartupFlowDecision.ShutdownInvalidState)
                {
                    var logger = _mainWindowScope.ServiceProvider.GetRequiredService<ILogger<App>>();
                    LogInvalidInstallationState(logger, installationState);

                    MessageBox.Show(
                        "La instalación local presenta un estado inconsistente. Consulte al soporte técnico.",
                        "PosPlatform",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    Shutdown(InvalidInstallationStateExitCode);
                    return;
                }

                if (initialDecision == StartupFlowDecision.ShowSetupDialog)
                {
                    var setupWindow = _mainWindowScope.ServiceProvider.GetRequiredService<InitialSetupWindow>();
                    var setupCompleted = setupWindow.ShowDialog();

                    if (StartupFlowCoordinator.DecideForSetupDialogResult(setupCompleted) == StartupFlowDecision.ShutdownCancelled)
                    {
                        Shutdown(0);
                        return;
                    }
                }

                // BASIC-USR-01: garantiza que los Roles Manager/Cashier existan antes del login,
                // tanto para una instalación recién configurada (Setup solo crea Administrator) como
                // para una instalación existente que se actualiza a esta versión. Idempotente y sin
                // efecto una vez que ambos roles ya existen (ver StandardRoleSeedingService).
                var roleSeedingService = _mainWindowScope.ServiceProvider.GetRequiredService<IStandardRoleSeedingService>();
                await roleSeedingService.EnsureStandardRolesExistAsync(CancellationToken.None);

                await RunLoginFlowAsync();
            }
            catch (LocalDatabaseInitializationException ex)
            {
                var logger = _mainWindowScope?.ServiceProvider.GetService<ILogger<App>>()
                    ?? _host?.Services.GetService<ILogger<App>>();
                if (logger is not null)
                {
                    LogDatabaseInitializationFailure(logger, ex);
                }

                MessageBox.Show(
                    "No fue posible preparar la base de datos local. Consulte al soporte técnico.",
                    "PosPlatform",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-2);
            }
            catch (Exception ex)
            {
                var logger = _host?.Services.GetService<ILogger<App>>();
                if (logger is not null)
                {
                    LogStartupFailure(logger, ex);
                }

                MessageBox.Show(
                    $"No fue posible iniciar la aplicación.\n\n{ex.Message}",
                    "PosPlatform",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(-1);
            }
        }

        // Muestra la ventana correspondiente a un estado de enforcement restrictivo confirmado
        // (Suspended/CredentialInvalid/Decommissioned). Cada ventana se resuelve por sí misma
        // (heartbeat exitoso, recuperación de credencial o activación como nueva Installation);
        // este método solo decide cuál mostrar (sección 21-23 de la tarea). Cada ventana también
        // expone "Cerrar caja abierta" (corrección: sección 7-9): mientras la ventana restrictiva
        // sigue abierta, ese evento se atiende con OnRestrictedFlowCloseOpenRegisterRequested sin
        // resolver el diálogo ni exponer el resto del POS.
        private bool? ShowRestrictedFlowDialog(StartupFlowDecision decision)
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException(
                    "El scope principal no está disponible para mostrar el flujo de enforcement.");
            }

            switch (decision)
            {
                case StartupFlowDecision.ShowSuspensionDialog:
                {
                    var window = _mainWindowScope.ServiceProvider.GetRequiredService<SuspensionWindow>();
                    window.CloseOpenRegisterRequested += OnRestrictedFlowCloseOpenRegisterRequested;

                    try
                    {
                        return window.ShowDialog();
                    }
                    finally
                    {
                        window.CloseOpenRegisterRequested -= OnRestrictedFlowCloseOpenRegisterRequested;
                    }
                }

                case StartupFlowDecision.ShowCredentialRecoveryDialog:
                {
                    var window = _mainWindowScope.ServiceProvider.GetRequiredService<CredentialRecoveryWindow>();
                    window.CloseOpenRegisterRequested += OnRestrictedFlowCloseOpenRegisterRequested;

                    try
                    {
                        return window.ShowDialog();
                    }
                    finally
                    {
                        window.CloseOpenRegisterRequested -= OnRestrictedFlowCloseOpenRegisterRequested;
                    }
                }

                case StartupFlowDecision.ShowNewInstallationActivationDialog:
                {
                    var window = _mainWindowScope.ServiceProvider.GetRequiredService<NewInstallationActivationWindow>();
                    window.CloseOpenRegisterRequested += OnRestrictedFlowCloseOpenRegisterRequested;

                    try
                    {
                        return window.ShowDialog();
                    }
                    finally
                    {
                        window.CloseOpenRegisterRequested -= OnRestrictedFlowCloseOpenRegisterRequested;
                    }
                }

                default:
                    return true;
            }
        }

        // Corrección (sección 7-9 de la tarea de corrección): permite cerrar una caja que ya
        // estaba abierta desde una pantalla restrictiva (Suspendido/Credencial inválida/Dado de
        // baja) mostrada antes del login, sin exponer el resto del POS. Reutiliza exactamente el
        // mismo LoginWindow (autenticación local existente, con sus mismos permisos) y el mismo
        // CloseRegisterSessionWindow que el flujo normal (sección 8: "reutilizar autenticación
        // local y/o UX de cierre de caja existente"). La ventana restrictiva (sender) permanece
        // abierta como Owner de ambos diálogos anidados y nunca se resuelve como completada: al
        // volver aquí el usuario sigue viendo la misma pantalla restrictiva (sección 9: "regresar a
        // la pantalla restrictiva después"). Cerrar la caja nunca invoca ClearAsync ni ninguna otra
        // vía que levante el enforcement (sección 20): el estado sigue siendo el mismo al volver.
        private async void OnRestrictedFlowCloseOpenRegisterRequested(object? sender, EventArgs e)
        {
            if (_mainWindowScope is null || sender is not Window ownerWindow)
            {
                return;
            }

            var loginWindow = _mainWindowScope.ServiceProvider.GetRequiredService<LoginWindow>();
            loginWindow.Owner = ownerWindow;
            var loginResult = loginWindow.ShowDialog();

            if (loginResult != true)
            {
                return;
            }

            try
            {
                var registerSessionService = _mainWindowScope.ServiceProvider.GetRequiredService<IRegisterSessionService>();
                var statusResult = await registerSessionService.GetCurrentAsync();

                if (statusResult.Status == RegisterSessionStatus.Open)
                {
                    var closeWindow = _mainWindowScope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>();
                    closeWindow.Owner = ownerWindow;
                    await closeWindow.LoadAsync();
                    closeWindow.ShowDialog();
                }
                else
                {
                    MessageBox.Show(
                        ownerWindow,
                        "No hay una caja abierta para cerrar.",
                        "PosPlatform",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            finally
            {
                // Nunca se permanece autenticado tras volver a la pantalla restrictiva: esta acción
                // es exclusivamente "cerrar la caja abierta", no un inicio de sesión normal
                // (sección 9: "NO exponer funcionalidad normal de venta/inventario/catálogo").
                ClearRegisterAndUserSessions();
            }
        }

        // Muestra LoginWindow y decide el siguiente paso según su resultado. Se invoca al
        // arrancar (tras setup/Initialized) y de nuevo tras cada logout, siempre reutilizando
        // _mainWindowScope: nunca se crea un segundo Host ni un segundo scope principal.
        private async Task RunLoginFlowAsync()
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException("El scope principal no está disponible para mostrar LoginWindow.");
            }

            var loginWindow = _mainWindowScope.ServiceProvider.GetRequiredService<LoginWindow>();
            var loginDialogResult = loginWindow.ShowDialog();

            if (StartupFlowCoordinator.DecideForLoginDialogResult(loginDialogResult) == StartupFlowDecision.ShutdownCancelled)
            {
                var session = _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentUserSession>();
                session.Clear();

                Shutdown(0);
                return;
            }

            await RunRegisterSessionFlowAsync();
        }

        // Tras un login exitoso, detecta si el usuario ya tiene una caja abierta (recuperación
        // tras un cierre inesperado incluida) antes de decidir entre MainWindow y
        // OpenRegisterSessionWindow.
        private async Task RunRegisterSessionFlowAsync()
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException("El scope principal no está disponible para consultar la caja.");
            }

            var registerSessionService = _mainWindowScope.ServiceProvider.GetRequiredService<IRegisterSessionService>();
            var statusResult = await registerSessionService.GetCurrentAsync();

            switch (StartupFlowCoordinator.DecideForRegisterSessionStatus(statusResult.Status))
            {
                case StartupFlowDecision.ShowMainWindowAfterLogin:
                    ShowMainWindow();
                    break;

                case StartupFlowDecision.ShowOpenRegisterSessionDialog:
                    await RunOpenRegisterSessionFlowAsync();
                    break;

                default:
                    var logger = _mainWindowScope.ServiceProvider.GetRequiredService<ILogger<App>>();
                    LogInvalidRegisterSessionState(logger);

                    MessageBox.Show(
                        "La caja presenta un estado inconsistente. Consulte al soporte técnico.",
                        "PosPlatform",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    var session = _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentUserSession>();
                    session.Clear();

                    Shutdown(InvalidRegisterSessionStateExitCode);
                    break;
            }
        }

        // Muestra OpenRegisterSessionWindow. Apertura exitosa continúa a MainWindow. "Cerrar
        // sesión" limpia ambas sesiones y vuelve a LoginWindow reutilizando el mismo Host y el
        // mismo scope principal, sin terminar el proceso. Cerrar la ventana con la X sí limpia
        // ambas sesiones y termina el proceso, igual que un login cancelado.
        private async Task RunOpenRegisterSessionFlowAsync()
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException(
                    "El scope principal no está disponible para mostrar OpenRegisterSessionWindow.");
            }

            var openWindow = _mainWindowScope.ServiceProvider.GetRequiredService<OpenRegisterSessionWindow>();
            openWindow.ShowDialog();

            var decision = StartupFlowCoordinator.DecideForOpenRegisterSessionResult(openWindow.Result);

            if (decision == StartupFlowDecision.ShowMainWindowAfterLogin && openWindow.OpenedSession is not null)
            {
                ShowMainWindow();
                return;
            }

            ClearRegisterAndUserSessions();

            if (decision == StartupFlowDecision.ShowLogin)
            {
                await RunLoginFlowAsync();
                return;
            }

            Shutdown(0);
        }

        // Limpieza defensiva de ambas sesiones y del carrito: ICurrentRegisterSession normalmente
        // ya está vacía en este punto (no hay caja abierta que bloquear) y el carrito ya debería
        // estarlo también (MainWindowViewModel bloquea el cierre de caja mientras tenga líneas),
        // pero se limpia igual para no dejar estado residual antes de volver a LoginWindow o de
        // terminar el proceso.
        private void ClearRegisterAndUserSessions()
        {
            if (_mainWindowScope is null)
            {
                return;
            }

            _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentSalesCart>().Clear();
            _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentRegisterSession>().Clear();
            _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Clear();
        }

        // Único punto donde se resuelve y muestra MainWindow, para el flujo de login exitoso
        // (inicial, tras logout o tras apertura/cierre de caja). Debe ejecutarse mientras
        // _mainWindowScope sigue vivo y antes de que cualquier código dependa de
        // Application.MainWindow.
        private void ShowMainWindow()
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException("El scope principal no está disponible para mostrar MainWindow.");
            }

            var mainWindow = _mainWindowScope.ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.LogoutRequested += OnMainWindowLogoutRequested;
            mainWindow.CloseRegisterRequested += OnMainWindowCloseRegisterRequested;
            mainWindow.NewProductRequested += OnMainWindowNewProductRequested;
            mainWindow.EditProductRequested += OnMainWindowEditProductRequested;
            mainWindow.CheckoutRequested += OnMainWindowCheckoutRequested;
            mainWindow.AdjustInventoryRequested += OnMainWindowAdjustInventoryRequested;
            mainWindow.NewUserRequested += OnMainWindowNewUserRequested;
            mainWindow.EditUserRequested += OnMainWindowEditUserRequested;
            mainWindow.CashInRequested += OnMainWindowCashInRequested;
            mainWindow.CashOutRequested += OnMainWindowCashOutRequested;

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        // Logout sin reiniciar el proceso: cierra la MainWindow actual (la sesión ya fue
        // limpiada por MainWindowViewModel antes de emitir el evento) y vuelve a mostrar
        // LoginWindow. No se crea un segundo Host ni un segundo scope principal. El logout ya
        // está bloqueado mientras la caja siga abierta (y por lo tanto mientras el carrito pueda
        // tener líneas), pero se limpia el carrito igual, de forma defensiva, sin depender de esa
        // garantía transitiva.
        private async void OnMainWindowLogoutRequested(object? sender, EventArgs e)
        {
            if (sender is MainWindow mainWindow)
            {
                mainWindow.LogoutRequested -= OnMainWindowLogoutRequested;
                mainWindow.CloseRegisterRequested -= OnMainWindowCloseRegisterRequested;
                mainWindow.NewProductRequested -= OnMainWindowNewProductRequested;
                mainWindow.EditProductRequested -= OnMainWindowEditProductRequested;
                mainWindow.CheckoutRequested -= OnMainWindowCheckoutRequested;
                mainWindow.AdjustInventoryRequested -= OnMainWindowAdjustInventoryRequested;
                mainWindow.NewUserRequested -= OnMainWindowNewUserRequested;
                mainWindow.EditUserRequested -= OnMainWindowEditUserRequested;
                mainWindow.CashInRequested -= OnMainWindowCashInRequested;
                mainWindow.CashOutRequested -= OnMainWindowCashOutRequested;
            }

            _mainWindowScope?.ServiceProvider.GetService<ICurrentSalesCart>()?.Clear();

            // Evita que cerrar la MainWindow actual dispare el apagado automático de
            // ShutdownMode.OnMainWindowClose antes de que LoginWindow pueda mostrarse.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            (sender as MainWindow)?.Close();

            await RunLoginFlowAsync();
        }

        // Muestra CloseRegisterSessionWindow sobre MainWindow (que permanece abierta como owner
        // mientras no se confirme el cierre). Solo si el cierre se confirma y persiste se cierra
        // MainWindow y se regresa a OpenRegisterSessionWindow; cancelar o cerrar con la X no
        // tiene efecto alguno sobre la caja.
        private async void OnMainWindowCloseRegisterRequested(object? sender, EventArgs e)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var closeWindow = _mainWindowScope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>();
            closeWindow.Owner = mainWindow;
            await closeWindow.LoadAsync();
            var dialogResult = closeWindow.ShowDialog();

            if (dialogResult != true)
            {
                return;
            }

            // MainWindowViewModel ya bloquea este flujo mientras el carrito tenga líneas; se
            // limpia igual, de forma defensiva, sin depender únicamente de esa garantía.
            _mainWindowScope.ServiceProvider.GetRequiredService<ICurrentSalesCart>().Clear();

            mainWindow.LogoutRequested -= OnMainWindowLogoutRequested;
            mainWindow.CloseRegisterRequested -= OnMainWindowCloseRegisterRequested;
            mainWindow.NewProductRequested -= OnMainWindowNewProductRequested;
            mainWindow.EditProductRequested -= OnMainWindowEditProductRequested;
            mainWindow.CheckoutRequested -= OnMainWindowCheckoutRequested;
            mainWindow.AdjustInventoryRequested -= OnMainWindowAdjustInventoryRequested;
            mainWindow.NewUserRequested -= OnMainWindowNewUserRequested;
            mainWindow.EditUserRequested -= OnMainWindowEditUserRequested;
            mainWindow.CashInRequested -= OnMainWindowCashInRequested;
            mainWindow.CashOutRequested -= OnMainWindowCashOutRequested;

            // Evita que cerrar la MainWindow actual dispare el apagado automático de
            // ShutdownMode.OnMainWindowClose antes de que OpenRegisterSessionWindow pueda mostrarse.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            mainWindow.Close();

            await RunOpenRegisterSessionFlowAsync();
        }

        // Muestra CreateProductWindow sobre MainWindow (que permanece abierta como owner). Si el
        // producto se crea con éxito, actualiza el buscador de MainWindow con el SKU recién
        // creado; cancelar o cerrar con la X no tiene efecto alguno.
        private void OnMainWindowNewProductRequested(object? sender, EventArgs e)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var createProductWindow = _mainWindowScope.ServiceProvider.GetRequiredService<CreateProductWindow>();
            createProductWindow.Owner = mainWindow;
            var dialogResult = createProductWindow.ShowDialog();

            if (dialogResult == true && createProductWindow.CreatedSku is { } sku)
            {
                mainWindow.ApplyProductCreated(sku);
            }
        }

        // Muestra EditProductWindow sobre MainWindow (que permanece abierta como owner). Carga el
        // producto antes de mostrar el diálogo; si la carga falla, la ventana igual se muestra con
        // el error visible (el usuario solo puede cancelar). Guardar y activar/desactivar se
        // aplican de inmediato dentro de EditProductWindow, no al cerrar: por eso el buscador de
        // MainWindow se refresca siempre que el producto se haya podido cargar, sin importar el
        // DialogResult final (Guardar vs Cancelar). Ajustar existencia ya no vive aquí (TAREA
        // 24G-FIX, sección 2): su único hogar es Inventario
        // (OnMainWindowAdjustInventoryRequested), reutilizando el mismo AdjustInventoryWindow.
        private async void OnMainWindowEditProductRequested(object? sender, ProductId productId)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var editProductWindow = _mainWindowScope.ServiceProvider.GetRequiredService<EditProductWindow>();
            editProductWindow.Owner = mainWindow;

            await editProductWindow.LoadAsync(productId);
            editProductWindow.ShowDialog();

            if (editProductWindow.LoadedSku is { } sku)
            {
                mainWindow.ApplyProductUpdated(sku);
            }
        }

        // Muestra AdjustInventoryWindow sobre MainWindow (que permanece abierta como owner), abierta
        // directamente desde Inventario (TAREA 24G, sección 16/17/19) — a diferencia del flujo de
        // Productos (OnEditProductWindowAdjustInventoryRequested), aquí no hay EditProductWindow de
        // por medio. Reutiliza exactamente el mismo AdjustInventoryWindow/ViewModel/servicio.
        private void OnMainWindowAdjustInventoryRequested(object? sender, InventoryCatalogItem item)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var adjustInventoryWindow = _mainWindowScope.ServiceProvider.GetRequiredService<AdjustInventoryWindow>();
            adjustInventoryWindow.Owner = mainWindow;
            adjustInventoryWindow.Load(item.ProductId, item.ProductName, item.Quantity);

            var dialogResult = adjustInventoryWindow.ShowDialog();

            if (dialogResult == true)
            {
                mainWindow.ApplyInventoryAdjusted();
            }
        }

        // Muestra CreateUserWindow sobre MainWindow (que permanece abierta como owner). Un usuario
        // creado con éxito refresca la lista de UserManagementView; cancelar o cerrar con la X no
        // tiene efecto alguno.
        private void OnMainWindowNewUserRequested(object? sender, EventArgs e)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var createUserWindow = _mainWindowScope.ServiceProvider.GetRequiredService<CreateUserWindow>();
            createUserWindow.Owner = mainWindow;
            var dialogResult = createUserWindow.ShowDialog();

            if (dialogResult == true)
            {
                mainWindow.ApplyUserChanged();
            }
        }

        // Muestra EditUserWindow sobre MainWindow (que permanece abierta como owner). Editar,
        // activar/desactivar y restablecer contraseña se aplican de inmediato dentro de
        // EditUserWindow, no al cerrar (mismo patrón que EditProductWindow/ToggleActive): por eso
        // la lista se refresca siempre que se haya aplicado al menos un cambio
        // (EditUserWindow.AnyChangeApplied), sin importar cómo se cerró la ventana.
        private async void OnMainWindowEditUserRequested(object? sender, UserId userId)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var editUserWindow = _mainWindowScope.ServiceProvider.GetRequiredService<EditUserWindow>();
            editUserWindow.Owner = mainWindow;

            await editUserWindow.LoadAsync(userId);
            editUserWindow.ShowDialog();

            if (editUserWindow.AnyChangeApplied)
            {
                mainWindow.ApplyUserChanged();
            }
        }

        // Muestra RecordCashMovementWindow sobre MainWindow (que permanece abierta como owner),
        // configurada para "Entrada de efectivo" (BASIC-CASH-01, sección 19-20). Un movimiento
        // registrado con éxito refresca el historial de Caja; cancelar o cerrar con la X no tiene
        // efecto alguno.
        private void OnMainWindowCashInRequested(object? sender, EventArgs e) =>
            ShowRecordCashMovementWindow(sender, CashMovementType.CashIn);

        // Igual patrón que OnMainWindowCashInRequested, pero para "Salida de efectivo".
        private void OnMainWindowCashOutRequested(object? sender, EventArgs e) =>
            ShowRecordCashMovementWindow(sender, CashMovementType.CashOut);

        private void ShowRecordCashMovementWindow(object? sender, CashMovementType type)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var recordWindow = _mainWindowScope.ServiceProvider.GetRequiredService<RecordCashMovementWindow>();
            recordWindow.Owner = mainWindow;
            recordWindow.Load(type);
            var dialogResult = recordWindow.ShowDialog();

            if (dialogResult == true)
            {
                mainWindow.ApplyCashMovementRecorded();
            }
        }

        // Muestra CheckoutWindow sobre MainWindow (que permanece abierta como owner). Un cobro
        // exitoso refresca SalesViewModel (carrito ya vacío: CheckoutService lo limpió dentro de su
        // único commit) y muestra el resumen de la venta; cancelar o cerrar con la X no tiene
        // efecto alguno sobre el carrito ni la caja.
        private async void OnMainWindowCheckoutRequested(object? sender, EventArgs e)
        {
            if (_mainWindowScope is null || sender is not MainWindow mainWindow)
            {
                return;
            }

            var checkoutWindow = _mainWindowScope.ServiceProvider.GetRequiredService<CheckoutWindow>();
            checkoutWindow.Owner = mainWindow;
            var dialogResult = checkoutWindow.ShowDialog();

            if (dialogResult != true || checkoutWindow.CompletedSummary is not { } summary)
            {
                return;
            }

            mainWindow.ApplyCheckoutCompleted();

            var paymentDetail = summary.PaymentMethod == CheckoutPaymentMethod.Cash
                ? $"\n\nForma de pago:\nEfectivo" +
                  $"\n\nEfectivo recibido:\n{summary.CashTendered.ToString("N2", CultureInfo.CurrentCulture)} {summary.Currency}" +
                  $"\n\nCambio:\n{summary.ChangeAmount.ToString("N2", CultureInfo.CurrentCulture)} {summary.Currency}"
                : $"\n\nForma de pago:\nTarjeta — manual" +
                  $"\n\nReferencia/autorización:\n{summary.CardReference}";

            // Impresión automática (sección 21-22 de la tarea): SIEMPRE después de que el checkout ya
            // confirmó su commit financiero (CompletedSummary solo existe si ICheckoutService tuvo
            // éxito). Un fallo de impresora aquí NUNCA deshace la venta ni sugiere reintentar el cobro
            // (sección 4): solo agrega una advertencia no destructiva al mismo mensaje de éxito.
            var printResult = await TryPrintReceiptAfterSaleAsync(summary);
            var printWarning = ToPrintWarningText(printResult);

            MessageBox.Show(
                "Venta completada correctamente." +
                $"\n\nVenta: {summary.SaleId}" +
                $"\n\nTotal:\n{summary.TotalAmount.ToString("N2", CultureInfo.CurrentCulture)} {summary.Currency}" +
                paymentDetail +
                printWarning,
                "PosPlatform",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private async Task<ReceiptPrintResult> TryPrintReceiptAfterSaleAsync(CheckoutSummary summary)
        {
            if (_mainWindowScope is null)
            {
                return ReceiptPrintResult.Of(ReceiptPrintResultStatus.Skipped);
            }

            var printingService = _mainWindowScope.ServiceProvider.GetRequiredService<IReceiptPrintingService>();

            decimal? cashTendered = summary.PaymentMethod == CheckoutPaymentMethod.Cash ? summary.CashTendered : null;
            decimal? changeDue = summary.PaymentMethod == CheckoutPaymentMethod.Cash ? summary.ChangeAmount : null;

            try
            {
                return await printingService.PrintAfterSaleAsync(summary.SaleId, cashTendered, changeDue);
            }
            catch (Exception ex)
            {
                LogReceiptPrintFailed(_mainWindowScope.ServiceProvider.GetRequiredService<ILogger<App>>(), ex);
                return ReceiptPrintResult.Of(ReceiptPrintResultStatus.PrintFailed);
            }
        }

        // Nunca "Venta fallida" (sección 4 de la tarea): el ticket se puede reimprimir desde
        // Historial, así que el mensaje siempre remite ahí en vez de sugerir repetir el cobro.
        private static string ToPrintWarningText(ReceiptPrintResult printResult) => printResult.Status switch
        {
            ReceiptPrintResultStatus.Success => string.Empty,
            ReceiptPrintResultStatus.Skipped => string.Empty,
            ReceiptPrintResultStatus.PrinterUnavailable or ReceiptPrintResultStatus.PrintFailed =>
                "\n\nNo fue posible imprimir el ticket. Puede reimprimirlo desde Historial de ventas.",
            _ => "\n\nNo fue posible imprimir el ticket. Puede reimprimirlo desde Historial de ventas.",
        };

        [LoggerMessage(Level = LogLevel.Error, Message = "Error inesperado al imprimir el ticket tras el cobro.")]
        private static partial void LogReceiptPrintFailed(ILogger logger, Exception exception);

        protected override void OnExit(ExitEventArgs e)
        {
            _mainWindowScope?.ServiceProvider.GetService<ICurrentSalesCart>()?.Clear();

            var session = _mainWindowScope?.ServiceProvider.GetService<ICurrentUserSession>();
            session?.Clear();

            _mainWindowScope?.Dispose();

            if (_host is not null)
            {
                _host.StopAsync().GetAwaiter().GetResult();
                _host.Dispose();
            }

            base.OnExit(e);
        }
    }
}
