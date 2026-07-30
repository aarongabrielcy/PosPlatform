using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Installation;
using Pos.Desktop.Setup;
using Pos.Infrastructure;
using Pos.Infrastructure.Storage;

namespace Pos.Desktop.Tests;

public class DependencyInjectionTests
{
    private sealed class FakeApplicationPathProvider : IApplicationPathProvider
    {
        public FakeApplicationPathProvider(string root)
        {
            DataDirectory = Path.Combine(root, "PosPlatform", "Data");
            DatabasePath = Path.Combine(DataDirectory, "pos.db");
            BackupDirectory = Path.Combine(DataDirectory, "Backups");
        }

        public string DataDirectory { get; }

        public string DatabasePath { get; }

        public string BackupDirectory { get; }

        public void EnsureDataDirectoryExists() =>
            throw new InvalidOperationException("No debe invocarse durante la prueba de composición.");

        public void EnsureBackupDirectoryExists() =>
            throw new InvalidOperationException("No debe invocarse durante la prueba de composición.");
    }

    private static ServiceProvider BuildProvider(FakeApplicationPathProvider pathProvider)
    {
        var services = new ServiceCollection();
        services.AddPosInfrastructure();

        // AddPosInfrastructure no registra infraestructura de logging (eso lo aporta
        // Host.CreateDefaultBuilder en producción); aquí se provee un ILogger<> mínimo para que
        // ValidateOnBuild pueda resolver InitialSetupViewModel/LocalDatabaseInitializer.
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IApplicationPathProvider>(pathProvider);

        services.AddTransient<MainWindow>();
        services.AddTransient<InitialSetupViewModel>();
        services.AddTransient<InitialSetupWindow>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true,
        });
    }

    // Los tipos Window de WPF (incluidos en esta composición como servicios transient) solo
    // pueden construirse en un hilo STA. ValidateOnBuild instancia todos los servicios
    // registrados al construir el contenedor, por lo que incluso BuildProvider debe ejecutarse
    // en ese hilo.
    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }
    }

    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), "PosPlatformDesktopDiTests_" + Guid.NewGuid());

    [Fact]
    public void MainWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<MainWindow>());
        });

    [Fact]
    public void InitialSetupViewModelResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<InitialSetupViewModel>());
        });

    [Fact]
    public void InitialSetupWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<InitialSetupWindow>());
        });

    [Fact]
    public void MainWindowAndInitialSetupWindowAreTransientAndProduceNewInstancesEachResolution() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            var firstMainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
            var secondMainWindow = scope.ServiceProvider.GetRequiredService<MainWindow>();
            Assert.NotSame(firstMainWindow, secondMainWindow);

            var firstSetupWindow = scope.ServiceProvider.GetRequiredService<InitialSetupWindow>();
            var secondSetupWindow = scope.ServiceProvider.GetRequiredService<InitialSetupWindow>();
            Assert.NotSame(firstSetupWindow, secondSetupWindow);
        });

    [Fact]
    public void IInstallationStateServiceResolvesWithinTheDesktopComposition() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInstallationStateService>());
        });

    [Fact]
    public void ResolvingDesktopWindowsDoesNotCreateAnySqliteFile() =>
        RunOnStaThread(() =>
        {
            var pathProvider = new FakeApplicationPathProvider(CreateTempRoot());
            using var provider = BuildProvider(pathProvider);
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<MainWindow>();
            scope.ServiceProvider.GetRequiredService<InitialSetupWindow>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        });
}
