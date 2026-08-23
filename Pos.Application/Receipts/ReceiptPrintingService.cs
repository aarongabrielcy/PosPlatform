using Pos.Application.Authentication;
using Pos.Application.Common.Time;
using Pos.Application.Organizations;
using Pos.Application.Sales.History;
using Pos.Domain.Common.Identifiers;
using Pos.Domain.Security;

namespace Pos.Application.Receipts;

// Orquesta la generación e impresión del ticket comercial (BASIC-PRN-01). Vive por completo FUERA
// de la transacción financiera de Checkout (sección 3/21/22): ICheckoutService ya confirmó el
// commit antes de que este servicio se invoque, y ninguna excepción/fallo de impresora puede
// deshacer Sale/Payment/Inventory. Reutiliza el mismo Receipt/IReceiptFormatter tanto para la
// impresión inicial como para el reimpreso (sección 11/28): la única diferencia es el origen de
// cashTendered/changeDue y el valor de IsReprint.
public sealed class ReceiptPrintingService : IReceiptPrintingService
{
    private readonly ICurrentUserSession _currentUserSession;
    private readonly ISalesHistoryQuery _salesHistoryQuery;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IReceiptFormatter _receiptFormatter;
    private readonly IReceiptPrinter _receiptPrinter;
    private readonly IReceiptPrinterOptionsProvider _printerOptionsProvider;
    private readonly IClock _clock;

    public ReceiptPrintingService(
        ICurrentUserSession currentUserSession,
        ISalesHistoryQuery salesHistoryQuery,
        IOrganizationRepository organizationRepository,
        IReceiptFormatter receiptFormatter,
        IReceiptPrinter receiptPrinter,
        IReceiptPrinterOptionsProvider printerOptionsProvider,
        IClock clock)
    {
        _currentUserSession = currentUserSession ?? throw new ArgumentNullException(nameof(currentUserSession));
        _salesHistoryQuery = salesHistoryQuery ?? throw new ArgumentNullException(nameof(salesHistoryQuery));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _receiptFormatter = receiptFormatter ?? throw new ArgumentNullException(nameof(receiptFormatter));
        _receiptPrinter = receiptPrinter ?? throw new ArgumentNullException(nameof(receiptPrinter));
        _printerOptionsProvider = printerOptionsProvider ?? throw new ArgumentNullException(nameof(printerOptionsProvider));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public async Task<ReceiptPrintResult> PrintAfterSaleAsync(
        Guid saleId, decimal? cashTendered, decimal? changeDue, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.Skipped);
        }

        if (!_printerOptionsProvider.Current.Enabled || !_printerOptionsProvider.Current.AutoPrint)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.Skipped);
        }

        return await PrintCoreAsync(user, saleId, isReprint: false, cashTendered, changeDue, cancellationToken);
    }

    public async Task<ReceiptPrintResult> ReprintAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ReprintReceipt))
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.NotAuthorized);
        }

        if (!_printerOptionsProvider.Current.Enabled)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.Skipped);
        }

        return await PrintCoreAsync(user, saleId, isReprint: true, cashTendered: null, changeDue: null, cancellationToken);
    }

    public async Task<ReceiptPrintResult> PrintTestAsync(CancellationToken cancellationToken = default)
    {
        var user = _currentUserSession.CurrentUser;

        if (user is null || !user.HasPermission(Permission.ManageSettings))
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.NotAuthorized);
        }

        var options = _printerOptionsProvider.Current;

        if (!options.Enabled)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.Skipped);
        }

        var receipt = ReceiptBuilder.BuildTestReceipt(options.PaperWidth, _clock.UtcNow);
        var formatted = _receiptFormatter.Format(receipt);
        var outcome = await _receiptPrinter.PrintAsync(formatted, cancellationToken);

        return ReceiptPrintResult.Of(outcome.Status switch
        {
            PrinterOutcomeStatus.Success => ReceiptPrintResultStatus.Success,
            PrinterOutcomeStatus.NotConfigured => ReceiptPrintResultStatus.Skipped,
            PrinterOutcomeStatus.PrinterUnavailable => ReceiptPrintResultStatus.PrinterUnavailable,
            PrinterOutcomeStatus.PrintFailed => ReceiptPrintResultStatus.PrintFailed,
            _ => ReceiptPrintResultStatus.PrintFailed,
        });
    }

    private async Task<ReceiptPrintResult> PrintCoreAsync(
        AuthenticatedUser user,
        Guid saleId,
        bool isReprint,
        decimal? cashTendered,
        decimal? changeDue,
        CancellationToken cancellationToken)
    {
        if (saleId == Guid.Empty)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.SaleNotFound);
        }

        var detail = await _salesHistoryQuery.GetDetailAsync(user.OrganizationId, new SaleId(saleId), cancellationToken);

        if (detail is null)
        {
            return ReceiptPrintResult.Of(ReceiptPrintResultStatus.SaleNotFound);
        }

        var organization = await _organizationRepository.GetByIdAsync(user.OrganizationId, cancellationToken);
        var businessName = string.IsNullOrWhiteSpace(organization?.Name) ? "PosPlatform" : organization!.Name;

        var receipt = ReceiptBuilder.Build(detail, businessName, isReprint, cashTendered, changeDue);
        var formatted = _receiptFormatter.Format(receipt);
        var outcome = await _receiptPrinter.PrintAsync(formatted, cancellationToken);

        return ReceiptPrintResult.Of(outcome.Status switch
        {
            PrinterOutcomeStatus.Success => ReceiptPrintResultStatus.Success,
            PrinterOutcomeStatus.NotConfigured => ReceiptPrintResultStatus.Skipped,
            PrinterOutcomeStatus.PrinterUnavailable => ReceiptPrintResultStatus.PrinterUnavailable,
            PrinterOutcomeStatus.PrintFailed => ReceiptPrintResultStatus.PrintFailed,
            _ => ReceiptPrintResultStatus.PrintFailed,
        });
    }
}
