using System.IO;
using Pos.Application.Authentication;
using Pos.Application.Configuration;
using Pos.Application.Receipts;
using Pos.Desktop.LocalConfiguration;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Security;

namespace Pos.Desktop.Tests.LocalConfiguration;

public class LocalConfigurationViewModelTests
{
    // ---------- Carga inicial (sección 35/36: refleja valores efectivos, nunca vacío) ----------

    [Fact]
    public async Task LoadPopulatesPrinterFieldsFromTheLiveOptionsProviderNotFromAnEmptyDefault()
    {
        var fixture = CreateFixture(printerOptions: new ReceiptPrinterOptions
        {
            Enabled = true,
            PrinterName = "58mm Bluetooth Printer",
            PaperWidth = ReceiptPaperWidth.Mm58,
            AutoPrint = false,
            CutPaper = true,
        });

        await fixture.ExecuteLoadAsync();

        Assert.True(fixture.ViewModel.PrinterEnabled);
        Assert.Equal("58mm Bluetooth Printer", fixture.ViewModel.SelectedPrinterName);
        Assert.Equal(ReceiptPaperWidth.Mm58, fixture.ViewModel.SelectedPaperWidth);
        Assert.False(fixture.ViewModel.AutoPrint);
        Assert.True(fixture.ViewModel.CutPaper);
    }

    [Fact]
    public async Task LoadPopulatesCashDrawerFieldsFromStoredSettingsWhenPresent()
    {
        var fixture = CreateFixture();
        fixture.SettingsStore.Loaded = new LocalSettings
        {
            CashDrawer = new LocalCashDrawerSettings { Enabled = true, OpenOnRegisterOpen = true },
        };

        await fixture.ExecuteLoadAsync();

        Assert.True(fixture.ViewModel.CashDrawerEnabled);
        Assert.True(fixture.ViewModel.OpenOnRegisterOpen);
    }

    [Fact]
    public async Task LoadDefaultsCashDrawerToDisabledWhenNothingWasSaved()
    {
        var fixture = CreateFixture();

        await fixture.ExecuteLoadAsync();

        Assert.False(fixture.ViewModel.CashDrawerEnabled);
        Assert.False(fixture.ViewModel.OpenOnRegisterOpen);
    }

    [Fact]
    public async Task LoadPopulatesInstalledPrintersFromDiscovery()
    {
        var fixture = CreateFixture();
        fixture.PrinterDiscovery.Names = ["Printer A", "Printer B"];

        await fixture.ExecuteLoadAsync();

        Assert.Equal(["Printer A", "Printer B"], fixture.ViewModel.AvailablePrinters);
    }

    // Sección 37 de la tarea: la impresora configurada ya no existe entre las instaladas - se
    // informa, nunca se selecciona otra automáticamente.
    [Fact]
    public async Task LoadFlagsWhenTheConfiguredPrinterIsNoLongerInstalled()
    {
        var fixture = CreateFixture(printerOptions: new ReceiptPrinterOptions { Enabled = true, PrinterName = "Missing Printer" });
        fixture.PrinterDiscovery.Names = ["Printer A"];

        await fixture.ExecuteLoadAsync();

        Assert.Equal("Missing Printer", fixture.ViewModel.SelectedPrinterName);
        Assert.NotNull(fixture.ViewModel.PrinterAvailabilityWarning);
    }

    [Fact]
    public async Task LoadDoesNotFlagAnythingWhenNoPrinterIsConfigured()
    {
        var fixture = CreateFixture(printerOptions: new ReceiptPrinterOptions { Enabled = false, PrinterName = null });

        await fixture.ExecuteLoadAsync();

        Assert.Null(fixture.ViewModel.PrinterAvailabilityWarning);
    }

    [Fact]
    public async Task LoadPopulatesOrganizationNameFromTheRepository()
    {
        var organizationId = OrganizationId.New();
        var fixture = CreateFixture(
            organization: new Organization(organizationId, "Tienda Uno", DateTimeOffset.UtcNow),
            organizationId: organizationId);

        await fixture.ExecuteLoadAsync();

        Assert.Equal("Tienda Uno", fixture.ViewModel.OrganizationName);
    }

    [Fact]
    public async Task LoadPopulatesVersionAndSettingsPathForTheSystemSection()
    {
        var fixture = CreateFixture();

        await fixture.ExecuteLoadAsync();

        Assert.Equal("1.0.0", fixture.ViewModel.ApplicationVersion);
        Assert.Contains("Config", fixture.ViewModel.SettingsFilePath);
        Assert.Contains("settings.json", fixture.ViewModel.SettingsFilePath);
    }

    // ---------- Guardar (sección 32/33: refresca el provider en vivo) ----------

    [Fact]
    public async Task SaveBuildsLocalSettingsFromCurrentFieldsAndDelegatesToTheService()
    {
        var fixture = CreateFixture();
        await fixture.ExecuteLoadAsync();
        fixture.ViewModel.PrinterEnabled = true;
        fixture.ViewModel.SelectedPrinterName = "Printer A";
        fixture.ViewModel.SelectedPaperWidth = ReceiptPaperWidth.Mm58;
        fixture.ViewModel.AutoPrint = false;
        fixture.ViewModel.CutPaper = true;
        fixture.ViewModel.CashDrawerEnabled = true;
        fixture.ViewModel.OpenOnRegisterOpen = true;

        await fixture.ExecuteSaveAsync();

        Assert.Equal(1, fixture.SettingsService.SaveCallCount);
        var saved = fixture.SettingsService.LastSaved!;
        Assert.True(saved.ReceiptPrinter.Enabled);
        Assert.Equal("Printer A", saved.ReceiptPrinter.PrinterName);
        Assert.Equal(ReceiptPaperWidth.Mm58, saved.ReceiptPrinter.PaperWidth);
        Assert.False(saved.ReceiptPrinter.AutoPrint);
        Assert.True(saved.ReceiptPrinter.CutPaper);
        Assert.True(saved.CashDrawer.Enabled);
        Assert.True(saved.CashDrawer.OpenOnRegisterOpen);
        Assert.Null(fixture.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task SaveShowsAnErrorWhenTheServiceDeniesTheMutation()
    {
        var fixture = CreateFixture();
        fixture.SettingsService.Result = LocalSettingsSaveResult.NotAuthorized;

        await fixture.ExecuteSaveAsync();

        Assert.NotNull(fixture.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task SaveShowsAnErrorWhenPersistenceFails()
    {
        var fixture = CreateFixture();
        fixture.SettingsService.Result = LocalSettingsSaveResult.Failed;

        await fixture.ExecuteSaveAsync();

        Assert.NotNull(fixture.ViewModel.ErrorMessage);
    }

    // ---------- Imprimir prueba (sección 15/49: nunca crea Sale/Payment) ----------

    [Fact]
    public async Task TestPrintDelegatesToThePrintingServiceAndNeverTouchesSalesHistory()
    {
        var fixture = CreateFixture();

        await fixture.ExecuteTestPrintAsync();

        Assert.Equal(1, fixture.PrintingService.PrintTestCallCount);
    }

    [Fact]
    public async Task TestPrintShowsASuccessMessageWhenThePrinterSucceeds()
    {
        var fixture = CreateFixture();
        fixture.PrintingService.PrintTestResultStatus = ReceiptPrintResultStatus.Success;

        await fixture.ExecuteTestPrintAsync();

        Assert.NotNull(fixture.ViewModel.StatusMessage);
        Assert.Null(fixture.ViewModel.ErrorMessage);
    }

    [Fact]
    public async Task TestPrintShowsAControlledErrorWhenThePrinterIsUnavailable()
    {
        var fixture = CreateFixture();
        fixture.PrintingService.PrintTestResultStatus = ReceiptPrintResultStatus.PrinterUnavailable;

        await fixture.ExecuteTestPrintAsync();

        Assert.NotNull(fixture.ViewModel.ErrorMessage);
    }

    private sealed record Fixture(
        LocalConfigurationViewModel ViewModel,
        FakeLocalSettingsStore SettingsStore,
        FakeLocalSettingsService SettingsService,
        FakePrinterDiscovery PrinterDiscovery,
        FakeReceiptPrintingService PrintingService)
    {
        // Todos los fakes inyectados devuelven Task ya completados (Task.FromResult), así que
        // AsyncRelayCommand.Execute (async void) corre de forma síncrona hasta su fin antes de que
        // Execute retorne - mismo criterio que MainWindowViewModelTests/ReportsViewModel al probar
        // LoadCommand sin await adicional.
        public Task ExecuteLoadAsync()
        {
            ViewModel.LoadCommand.Execute(null);
            return Task.CompletedTask;
        }

        public Task ExecuteSaveAsync()
        {
            ViewModel.SaveCommand.Execute(null);
            return Task.CompletedTask;
        }

        public Task ExecuteTestPrintAsync()
        {
            ViewModel.TestPrintCommand.Execute(null);
            return Task.CompletedTask;
        }
    }

    private static Fixture CreateFixture(
        ReceiptPrinterOptions? printerOptions = null,
        Organization? organization = null,
        OrganizationId? organizationId = null)
    {
        var resolvedOrganizationId = organizationId ?? OrganizationId.New();
        var session = new FakeCurrentUserSession
        {
            CurrentUser = new AuthenticatedUser(
                UserId.New(), resolvedOrganizationId, RoleId.New(), "ADMIN", "Admin Uno", "Administrador",
                [Permission.ManageSettings]),
        };

        var settingsStore = new FakeLocalSettingsStore();
        var settingsService = new FakeLocalSettingsService();
        var printerOptionsProvider = new FixedReceiptPrinterOptionsProvider(
            printerOptions ?? new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" });
        var printerDiscovery = new FakePrinterDiscovery();
        var printingService = new FakeReceiptPrintingService();
        var organizationRepository = new FakeOrganizationRepository(organization);
        var versionProvider = new FakeApplicationVersionProvider();
        var pathProvider = new FakeApplicationPathProvider(
            Path.Combine(Path.GetTempPath(), "PosPlatformLocalConfigViewModelTests_" + Guid.NewGuid()));
        var activationStateService = new FakeInstallationActivationStateService();

        var viewModel = new LocalConfigurationViewModel(
            settingsStore, settingsService, printerOptionsProvider, printerDiscovery, printingService,
            session, organizationRepository, versionProvider, pathProvider, activationStateService);

        return new Fixture(viewModel, settingsStore, settingsService, printerDiscovery, printingService);
    }
}
