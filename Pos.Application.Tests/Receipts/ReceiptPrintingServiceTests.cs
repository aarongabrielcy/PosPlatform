using Pos.Application.Authentication;
using Pos.Application.Receipts;
using Pos.Application.Sales.History;
using Pos.Application.Tests.Bootstrap;
using Pos.Application.Tests.Common.Time;
using Pos.Application.Tests.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Organizations;
using Pos.Domain.Sales;
using Pos.Domain.Security;

namespace Pos.Application.Tests.Receipts;

public sealed class ReceiptPrintingServiceTests
{
    private static readonly Guid SampleSaleId = Guid.NewGuid();
    private static readonly DateTimeOffset CompletedAtUtc = new(2026, 8, 20, 20, 15, 0, TimeSpan.Zero);

    // ---------- PrintAfterSaleAsync ----------

    [Fact]
    public async Task PrintAfterSaleWhenPrinterDisabledReturnsSkippedWithoutTouchingFormatterOrPrinter()
    {
        var fixture = CreateFixture(printerOptions: new ReceiptPrinterOptions { Enabled = false, AutoPrint = true });

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
        Assert.Equal(0, fixture.Formatter.FormatCallCount);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintAfterSaleWhenAutoPrintDisabledReturnsSkippedEvenIfPrinterEnabled()
    {
        var fixture = CreateFixture(printerOptions: new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20", AutoPrint = false });

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintAfterSaleWhenNotAuthenticatedReturnsSkipped()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintAfterSaleWhenSaleNotFoundReturnsSaleNotFound()
    {
        var fixture = CreateFixture(saleFound: false);

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.SaleNotFound, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintAfterSaleWhenPrinterSucceedsReturnsSuccessAndPassesCashFigures()
    {
        var fixture = CreateFixture();

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, cashTendered: 50m, changeDue: 20m);

        Assert.True(result.Success);
        Assert.False(fixture.Formatter.LastReceipt!.IsReprint);
        Assert.Equal(50m, fixture.Formatter.LastReceipt!.CashTendered);
        Assert.Equal(20m, fixture.Formatter.LastReceipt!.ChangeDue);
    }

    [Fact]
    public async Task PrintAfterSaleWhenPrinterUnavailableMapsToPrinterUnavailable()
    {
        var fixture = CreateFixture();
        fixture.Printer.ResultStatus = PrinterOutcomeStatus.PrinterUnavailable;

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.PrinterUnavailable, result.Status);
    }

    [Fact]
    public async Task PrintAfterSaleWhenPrintFailsMapsToPrintFailed()
    {
        var fixture = CreateFixture();
        fixture.Printer.ResultStatus = PrinterOutcomeStatus.PrintFailed;

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.PrintFailed, result.Status);
    }

    [Fact]
    public async Task PrintAfterSaleWhenPrinterReportsNotConfiguredMapsToSkipped()
    {
        var fixture = CreateFixture();
        fixture.Printer.ResultStatus = PrinterOutcomeStatus.NotConfigured;

        var result = await fixture.Service.PrintAfterSaleAsync(SampleSaleId, null, null);

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
    }

    // ---------- ReprintAsync ----------

    [Fact]
    public async Task ReprintWithoutPermissionReturnsNotAuthorizedWithoutTouchingPrinter()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewSalesHistory]);

        var result = await fixture.Service.ReprintAsync(SampleSaleId);

        Assert.Equal(ReceiptPrintResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task ReprintWithoutAuthenticatedUserReturnsNotAuthorized()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.ReprintAsync(SampleSaleId);

        Assert.Equal(ReceiptPrintResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task ReprintWithPermissionMarksTheReceiptAsReprintAndNeverCarriesCashFigures()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewSalesHistory, Permission.ReprintReceipt]);

        var result = await fixture.Service.ReprintAsync(SampleSaleId);

        Assert.True(result.Success);
        Assert.True(fixture.Formatter.LastReceipt!.IsReprint);
        Assert.Null(fixture.Formatter.LastReceipt!.CashTendered);
        Assert.Null(fixture.Formatter.LastReceipt!.ChangeDue);
    }

    [Fact]
    public async Task ReprintIgnoresAutoPrintFlagAndOnlyRequiresPrinterEnabled()
    {
        var fixture = CreateFixture(
            permissions: [Permission.ReprintReceipt],
            printerOptions: new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20", AutoPrint = false });

        var result = await fixture.Service.ReprintAsync(SampleSaleId);

        Assert.True(result.Success);
        Assert.Equal(1, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task ReprintWhenPrinterDisabledReturnsSkipped()
    {
        var fixture = CreateFixture(
            permissions: [Permission.ReprintReceipt],
            printerOptions: new ReceiptPrinterOptions { Enabled = false });

        var result = await fixture.Service.ReprintAsync(SampleSaleId);

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    // ---------- PrintTestAsync (BASIC-CFG-01, sección 15/16/49) ----------

    [Fact]
    public async Task PrintTestWithoutManageSettingsPermissionReturnsNotAuthorizedWithoutTouchingPrinter()
    {
        var fixture = CreateFixture(permissions: [Permission.ViewSalesHistory]);

        var result = await fixture.Service.PrintTestAsync();

        Assert.Equal(ReceiptPrintResultStatus.NotAuthorized, result.Status);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintTestWithoutAuthenticatedUserReturnsNotAuthorized()
    {
        var fixture = CreateFixture(authenticated: false);

        var result = await fixture.Service.PrintTestAsync();

        Assert.Equal(ReceiptPrintResultStatus.NotAuthorized, result.Status);
    }

    [Fact]
    public async Task PrintTestWhenPrinterDisabledReturnsSkippedWithoutTouchingFormatterOrPrinter()
    {
        var fixture = CreateFixture(
            permissions: [Permission.ManageSettings],
            printerOptions: new ReceiptPrinterOptions { Enabled = false });

        var result = await fixture.Service.PrintTestAsync();

        Assert.Equal(ReceiptPrintResultStatus.Skipped, result.Status);
        Assert.Equal(0, fixture.Formatter.FormatCallCount);
        Assert.Equal(0, fixture.Printer.PrintCallCount);
    }

    [Fact]
    public async Task PrintTestWhenEnabledSendsAHarmlessReceiptWithoutTouchingSalesHistory()
    {
        var fixture = CreateFixture(
            permissions: [Permission.ManageSettings],
            printerOptions: new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20" });

        var result = await fixture.Service.PrintTestAsync();

        Assert.True(result.Success);
        Assert.Equal(1, fixture.Printer.PrintCallCount);
        Assert.False(fixture.Formatter.LastReceipt!.IsReprint);
        Assert.Equal(0, fixture.Query.GetDetailCallCount);
    }

    private sealed record Fixture(
        ReceiptPrintingService Service, FakeReceiptFormatter Formatter, FakeReceiptPrinter Printer, FakeSalesHistoryQuery Query);

    private static Fixture CreateFixture(
        bool authenticated = true,
        IEnumerable<Permission>? permissions = null,
        bool saleFound = true,
        ReceiptPrinterOptions? printerOptions = null)
    {
        var organizationId = OrganizationId.New();
        var session = new FakeCurrentUserSession();

        if (authenticated)
        {
            session.CurrentUser = new AuthenticatedUser(
                UserId.New(), organizationId, RoleId.New(), "JPEREZ", "Ana Cajera", "Cajero",
                permissions ?? [Permission.ViewSalesHistory, Permission.ReprintReceipt]);
        }

        var query = new FakeSalesHistoryQuery(detail: saleFound ? BuildDetail() : null);
        var organizationRepository = new FakeOrganizationRepository([new Organization(organizationId, "PosPlatform Demo", CompletedAtUtc)]);
        var formatter = new FakeReceiptFormatter();
        var printer = new FakeReceiptPrinter();

        var service = new ReceiptPrintingService(
            session, query, organizationRepository, formatter, printer,
            new FixedReceiptPrinterOptionsProvider(
                printerOptions ?? new ReceiptPrinterOptions { Enabled = true, PrinterName = "TM-T20", AutoPrint = true }),
            new FakeClock(CompletedAtUtc));

        return new Fixture(service, formatter, printer, query);
    }

    private static SaleHistoryDetail BuildDetail() => new(
        new SaleId(SampleSaleId),
        SaleStatus.Completed,
        CompletedAtUtc.AddMinutes(-2),
        CompletedAtUtc,
        UserId.New(),
        "Ana Cajera",
        RegisterId.New(),
        "Caja 1",
        RegisterSessionId.New(),
        20.00m,
        20.00m,
        "MXN",
        [new SaleHistoryDetailLine("SKU-001", "Producto de prueba", 2m, 10.00m, 20.00m, "MXN")],
        [new SaleHistoryDetailPayment(PaymentMethod.Cash, 20.00m, "MXN", CompletedAtUtc, null)]);
}
