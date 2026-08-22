using Pos.Application.Receipts;

namespace Pos.Desktop.Tests.Sales.History;

internal sealed class FakeReceiptPrintingService : IReceiptPrintingService
{
    public ReceiptPrintResultStatus PrintAfterSaleResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public ReceiptPrintResultStatus ReprintResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public int ReprintCallCount { get; private set; }

    public Guid? LastReprintSaleId { get; private set; }

    public Task<ReceiptPrintResult> PrintAfterSaleAsync(
        Guid saleId, decimal? cashTendered, decimal? changeDue, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptPrintResult.Of(PrintAfterSaleResultStatus));

    public Task<ReceiptPrintResult> ReprintAsync(Guid saleId, CancellationToken cancellationToken = default)
    {
        ReprintCallCount++;
        LastReprintSaleId = saleId;

        return Task.FromResult(ReceiptPrintResult.Of(ReprintResultStatus));
    }
}
