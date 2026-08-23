using Pos.Application.Receipts;

namespace Pos.Desktop.Tests.LocalConfiguration;

internal sealed class FakeReceiptPrintingService : IReceiptPrintingService
{
    public ReceiptPrintResultStatus PrintAfterSaleResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public ReceiptPrintResultStatus ReprintResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public ReceiptPrintResultStatus PrintTestResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public int PrintTestCallCount { get; private set; }

    public Task<ReceiptPrintResult> PrintAfterSaleAsync(
        Guid saleId, decimal? cashTendered, decimal? changeDue, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptPrintResult.Of(PrintAfterSaleResultStatus));

    public Task<ReceiptPrintResult> ReprintAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptPrintResult.Of(ReprintResultStatus));

    public Task<ReceiptPrintResult> PrintTestAsync(CancellationToken cancellationToken = default)
    {
        PrintTestCallCount++;

        return Task.FromResult(ReceiptPrintResult.Of(PrintTestResultStatus));
    }
}
