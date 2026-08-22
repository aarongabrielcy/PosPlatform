using Pos.Application.Receipts;

namespace Pos.Application.Tests.Receipts;

internal sealed class FakeReceiptFormatter : IReceiptFormatter
{
    public Receipt? LastReceipt { get; private set; }

    public int FormatCallCount { get; private set; }

    public FormattedReceipt Format(Receipt receipt)
    {
        FormatCallCount++;
        LastReceipt = receipt;

        return new FormattedReceipt([0x01]);
    }
}
