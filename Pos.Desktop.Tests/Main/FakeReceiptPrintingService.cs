using Pos.Application.Receipts;

namespace Pos.Desktop.Tests.Main;

internal sealed class FakeReceiptPrintingService : IReceiptPrintingService
{
    public ReceiptPrintResultStatus PrintAfterSaleResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public ReceiptPrintResultStatus ReprintResultStatus { get; set; } = ReceiptPrintResultStatus.Success;

    public Task<ReceiptPrintResult> PrintAfterSaleAsync(
        Guid saleId, decimal? cashTendered, decimal? changeDue, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptPrintResult.Of(PrintAfterSaleResultStatus));

    public Task<ReceiptPrintResult> ReprintAsync(Guid saleId, CancellationToken cancellationToken = default) =>
        Task.FromResult(ReceiptPrintResult.Of(ReprintResultStatus));
}
