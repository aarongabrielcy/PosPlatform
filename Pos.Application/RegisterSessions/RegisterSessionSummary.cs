namespace Pos.Application.RegisterSessions;

// Resumen inmutable devuelto tras cerrar una caja. En esta fase no existen ventas ni
// movimientos: ExpectedAmount siempre coincide con OpeningAmount.
public sealed class RegisterSessionSummary
{
    public string RegisterName { get; }

    public DateTimeOffset OpenedAtUtc { get; }

    public DateTimeOffset ClosedAtUtc { get; }

    public string OpenedByDisplayName { get; }

    public string ClosedByDisplayName { get; }

    public decimal OpeningAmount { get; }

    public decimal ClosingAmount { get; }

    public decimal ExpectedAmount { get; }

    public decimal Difference { get; }

    public string Currency { get; }

    public RegisterSessionSummary(
        string registerName,
        DateTimeOffset openedAtUtc,
        DateTimeOffset closedAtUtc,
        string openedByDisplayName,
        string closedByDisplayName,
        decimal openingAmount,
        decimal closingAmount,
        decimal expectedAmount,
        decimal difference,
        string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(openedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(closedByDisplayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        RegisterName = registerName;
        OpenedAtUtc = openedAtUtc;
        ClosedAtUtc = closedAtUtc;
        OpenedByDisplayName = openedByDisplayName;
        ClosedByDisplayName = closedByDisplayName;
        OpeningAmount = openingAmount;
        ClosingAmount = closingAmount;
        ExpectedAmount = expectedAmount;
        Difference = difference;
        Currency = currency;
    }
}
