using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pos.Application.Activation;
using Pos.Application.Authentication;
using Pos.Application.Configuration;
using Pos.Application.Installation;
using Pos.Application.Receipts;
using Pos.Application.RegisterSessions;
using Pos.Desktop.Activation;
using Pos.Desktop.AdministrativeNotifications;
using Pos.Desktop.Audit.Products;
using Pos.Desktop.Configuration;
using Pos.Desktop.Dashboard;
using Pos.Desktop.Enforcement;
using Pos.Desktop.Inventory;
using Pos.Desktop.LocalConfiguration;
using Pos.Desktop.Login;
using Pos.Desktop.Main;
using Pos.Desktop.Products.Catalog;
using Pos.Desktop.Register;
using Pos.Desktop.RegisterSessions;
using Pos.Desktop.Reports;
using Pos.Desktop.Sales;
using Pos.Desktop.Sales.History;
using Pos.Desktop.Setup;
using Pos.Desktop.Users;
using Pos.Hardware.EscPos;
using Pos.Hardware.Printing;
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

        // BASIC-PRN-01/BASIC-CFG-01: mismo registro que App.xaml.cs (ver comentario allí sobre por
        // qué IReceiptPrintingService/ILocalSettingsService no viven en AddPosInfrastructure).
        // Enabled=false: construir estos servicios nunca debe tocar el spooler real de Windows
        // durante ValidateOnBuild.
        services.AddSingleton<IReceiptPrinterOptionsProvider>(sp =>
            new ReceiptPrinterOptionsProvider(new ReceiptPrinterOptions { Enabled = false }, sp.GetRequiredService<ILocalSettingsStore>()));
        services.AddSingleton<IReceiptFormatter, EscPosReceiptFormatter>();
        services.AddSingleton<IReceiptPrinter, WindowsSpoolReceiptPrinter>();
        services.AddSingleton<IPrinterDiscovery, WindowsPrinterDiscovery>();
        services.AddScoped<IReceiptPrintingService, ReceiptPrintingService>();
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
    public void ActivationViewModelResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ActivationViewModel>());
        });

    [Fact]
    public void ActivationWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ActivationWindow>());
        });

    [Fact]
    public void IInstallationActivationStateServiceResolvesWithinTheDesktopComposition() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IInstallationActivationStateService>());
        });

    [Fact]
    public void SuspensionWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<SuspensionWindow>());
        });

    [Fact]
    public void CredentialRecoveryWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CredentialRecoveryWindow>());
        });

    [Fact]
    public void NewInstallationActivationWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<NewInstallationActivationWindow>());
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
    public void LoginViewModelResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<LoginViewModel>());
        });

    [Fact]
    public void LoginWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<LoginWindow>());
        });

    [Fact]
    public void MainWindowViewModelResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<MainWindowViewModel>());
        });

    [Fact]
    public void ChildShellViewModelsResolveWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<DashboardViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<SalesViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<SalesHistoryViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProductsViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<InventoryViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<RegisterViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<UserManagementViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<ProductAuditViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<LocalConfigurationViewModel>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<NotificationCenterViewModel>());
        });

    [Fact]
    public void CreateUserWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CreateUserWindow>());
        });

    [Fact]
    public void EditUserWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<EditUserWindow>());
        });

    [Fact]
    public void LoginWindowAndMainWindowProduceNewInstancesEachResolution() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            var firstLoginWindow = scope.ServiceProvider.GetRequiredService<LoginWindow>();
            var secondLoginWindow = scope.ServiceProvider.GetRequiredService<LoginWindow>();
            Assert.NotSame(firstLoginWindow, secondLoginWindow);
        });

    [Fact]
    public void OpenRegisterSessionWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<OpenRegisterSessionWindow>());
        });

    [Fact]
    public void CloseRegisterSessionWindowResolvesWithinAScope() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            Assert.NotNull(scope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>());
        });

    [Fact]
    public void OpenAndCloseRegisterSessionWindowsProduceNewInstancesEachResolution() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));
            using var scope = provider.CreateScope();

            var firstOpenWindow = scope.ServiceProvider.GetRequiredService<OpenRegisterSessionWindow>();
            var secondOpenWindow = scope.ServiceProvider.GetRequiredService<OpenRegisterSessionWindow>();
            Assert.NotSame(firstOpenWindow, secondOpenWindow);

            var firstCloseWindow = scope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>();
            var secondCloseWindow = scope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>();
            Assert.NotSame(firstCloseWindow, secondCloseWindow);
        });

    [Fact]
    public void MainWindowAndLoginWindowShareTheSameSingletonCurrentRegisterSessionAcrossScopes() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));

            var rootSession = provider.GetRequiredService<ICurrentRegisterSession>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var sessionA = scopeA.ServiceProvider.GetRequiredService<ICurrentRegisterSession>();
            var sessionB = scopeB.ServiceProvider.GetRequiredService<ICurrentRegisterSession>();

            Assert.Same(rootSession, sessionA);
            Assert.Same(rootSession, sessionB);
        });

    [Fact]
    public void MainWindowAndLoginWindowShareTheSameSingletonCurrentUserSessionAcrossScopes() =>
        RunOnStaThread(() =>
        {
            using var provider = BuildProvider(new FakeApplicationPathProvider(CreateTempRoot()));

            var rootSession = provider.GetRequiredService<ICurrentUserSession>();

            using var scopeA = provider.CreateScope();
            using var scopeB = provider.CreateScope();

            var sessionA = scopeA.ServiceProvider.GetRequiredService<ICurrentUserSession>();
            var sessionB = scopeB.ServiceProvider.GetRequiredService<ICurrentUserSession>();

            Assert.Same(rootSession, sessionA);
            Assert.Same(rootSession, sessionB);
        });

    [Fact]
    public void ResolvingDesktopWindowsDoesNotCreateAnySqliteFile() =>
        RunOnStaThread(() =>
        {
            var pathProvider = new FakeApplicationPathProvider(CreateTempRoot());
            using var provider = BuildProvider(pathProvider);
            using var scope = provider.CreateScope();

            scope.ServiceProvider.GetRequiredService<ActivationWindow>();
            scope.ServiceProvider.GetRequiredService<MainWindow>();
            scope.ServiceProvider.GetRequiredService<InitialSetupWindow>();
            scope.ServiceProvider.GetRequiredService<LoginWindow>();
            scope.ServiceProvider.GetRequiredService<OpenRegisterSessionWindow>();
            scope.ServiceProvider.GetRequiredService<CloseRegisterSessionWindow>();
            scope.ServiceProvider.GetRequiredService<CreateUserWindow>();
            scope.ServiceProvider.GetRequiredService<EditUserWindow>();

            Assert.False(Directory.Exists(pathProvider.DataDirectory));
            Assert.False(File.Exists(pathProvider.DatabasePath));
        });
}
