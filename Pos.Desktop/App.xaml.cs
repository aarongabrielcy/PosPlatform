using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pos.Application.Authentication;
using Pos.Application.Installation;
using Pos.Desktop.Login;
using Pos.Desktop.Main;
using Pos.Desktop.Setup;
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

        private IHost? _host;
        private IServiceScope? _mainWindowScope;

        [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo al iniciar la aplicación.")]
        private static partial void LogStartupFailure(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo al inicializar la base de datos local.")]
        private static partial void LogDatabaseInitializationFailure(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "La instalación local presenta un estado inconsistente ({InstallationState}).")]
        private static partial void LogInvalidInstallationState(ILogger logger, InstallationState installationState);

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
                _host = Host.CreateDefaultBuilder()
                    .UseDefaultServiceProvider(options =>
                    {
                        options.ValidateScopes = true;
                        options.ValidateOnBuild = true;
                    })
                    .ConfigureServices(services =>
                    {
                        services.AddPosInfrastructure();
                        services.AddTransient<MainWindow>();
                        services.AddTransient<MainWindowViewModel>();
                        services.AddTransient<InitialSetupViewModel>();
                        services.AddTransient<InitialSetupWindow>();
                        services.AddTransient<LoginViewModel>();
                        services.AddTransient<LoginWindow>();
                    })
                    .Build();

                // No hay IHostedService registrado; Start()/Stop() son síncronos y no bloquean
                // el hilo de UI de forma perceptible.
                _host.Start();

                var pathProvider = _host.Services.GetRequiredService<IApplicationPathProvider>();
                pathProvider.EnsureDataDirectoryExists();

                _mainWindowScope = _host.Services.CreateScope();

                var initializer = _mainWindowScope.ServiceProvider.GetRequiredService<ILocalDatabaseInitializer>();
                await initializer.InitializeAsync();

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

                RunLoginFlow();
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

        // Muestra LoginWindow y decide el siguiente paso según su resultado. Se invoca al
        // arrancar (tras setup/Initialized) y de nuevo tras cada logout, siempre reutilizando
        // _mainWindowScope: nunca se crea un segundo Host ni un segundo scope principal.
        private void RunLoginFlow()
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

            ShowMainWindow();
        }

        // Único punto donde se resuelve y muestra MainWindow, para el flujo de login exitoso
        // (inicial o tras logout). Debe ejecutarse mientras _mainWindowScope sigue vivo y antes
        // de que cualquier código dependa de Application.MainWindow.
        private void ShowMainWindow()
        {
            if (_mainWindowScope is null)
            {
                throw new InvalidOperationException("El scope principal no está disponible para mostrar MainWindow.");
            }

            var mainWindow = _mainWindowScope.ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.LogoutRequested += OnMainWindowLogoutRequested;

            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }

        // Logout sin reiniciar el proceso: cierra la MainWindow actual (la sesión ya fue
        // limpiada por MainWindowViewModel antes de emitir el evento) y vuelve a mostrar
        // LoginWindow. No se crea un segundo Host ni un segundo scope principal.
        private void OnMainWindowLogoutRequested(object? sender, EventArgs e)
        {
            if (sender is MainWindow mainWindow)
            {
                mainWindow.LogoutRequested -= OnMainWindowLogoutRequested;
            }

            // Evita que cerrar la MainWindow actual dispare el apagado automático de
            // ShutdownMode.OnMainWindowClose antes de que LoginWindow pueda mostrarse.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            (sender as MainWindow)?.Close();

            RunLoginFlow();
        }

        protected override void OnExit(ExitEventArgs e)
        {
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
