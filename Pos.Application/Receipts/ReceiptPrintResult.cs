namespace Pos.Application.Receipts;

public sealed class ReceiptPrintResult
{
    public ReceiptPrintResultStatus Status { get; }

    public bool Success => Status == ReceiptPrintResultStatus.Success;

    private ReceiptPrintResult(ReceiptPrintResultStatus status)
    {
        Status = status;
    }

    public static ReceiptPrintResult Of(ReceiptPrintResultStatus status) => new(status);
}
