using Pos.Application.Receipts;

namespace Pos.Infrastructure.Tests.Integration;

internal sealed class FakeReceiptPrinter : IReceiptPrinter
{
    public PrinterOutcomeStatus ResultStatus { get; set; } = PrinterOutcomeStatus.Success;

    public int PrintCallCount { get; private set; }

    public Task<PrinterOutcome> PrintAsync(FormattedReceipt receipt, CancellationToken cancellationToken = default)
    {
        PrintCallCount++;

        return Task.FromResult(ResultStatus switch
        {
            PrinterOutcomeStatus.Success => PrinterOutcome.Success(),
            PrinterOutcomeStatus.NotConfigured => PrinterOutcome.NotConfigured(),
            PrinterOutcomeStatus.PrinterUnavailable => PrinterOutcome.Unavailable("printer offline (test)"),
            _ => PrinterOutcome.Failed("write failed (test)"),
        });
    }
}
