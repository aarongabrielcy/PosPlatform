using Pos.Application.Receipts;

namespace Pos.Infrastructure.Tests.Integration;

internal sealed class FakeReceiptFormatter : IReceiptFormatter
{
    public Receipt? LastReceipt { get; private set; }

    public FormattedReceipt Format(Receipt receipt)
    {
        LastReceipt = receipt;
        return new FormattedReceipt([0x01]);
    }
}
