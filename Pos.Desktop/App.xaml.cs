using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pos.Infrastructure;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        private IHost? _host;
        private IServiceScope? _mainWindowScope;

        [LoggerMessage(Level = LogLevel.Critical, Message = "Fallo al iniciar la aplicación.")]
        private static partial void LogStartupFailure(ILogger logger, Exception exception);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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
                    })
                    .Build();

                // No hay IHostedService registrado; Start()/Stop() son síncronos y no bloquean
                // el hilo de UI de forma perceptible.
                _host.Start();

                var pathProvider = _host.Services.GetRequiredService<IApplicationPathProvider>();
                pathProvider.EnsureDataDirectoryExists();

                _mainWindowScope = _host.Services.CreateScope();
                var mainWindow = _mainWindowScope.ServiceProvider.GetRequiredService<MainWindow>();

                MainWindow = mainWindow;
                mainWindow.Show();
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

        protected override void OnExit(ExitEventArgs e)
        {
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
