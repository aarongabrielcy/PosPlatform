using System.Globalization;
using Pos.Application.CashMovements;
using Pos.Domain.CashMovements;

namespace Pos.Desktop.Register;

// Proyección de presentación de un CashMovementEntry para el resumen de Caja (sección 21 de la
// tarea): tipo, monto, motivo, operador y fecha/hora, sin pretender ser una lista paginada.
public sealed class CashMovementRowViewModel
{
    public CashMovementRowViewModel(CashMovementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        TypeText = entry.Type == CashMovementType.CashIn ? "Entrada" : "Salida";
        AmountText = $"{entry.Amount.ToString("N2", CultureInfo.CurrentCulture)} {entry.Currency}";
        Reason = entry.Reason;
        ActorDisplayName = entry.ActorDisplayName;
        CreatedAtText = entry.CreatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    public string TypeText { get; }

    public string AmountText { get; }

    public string Reason { get; }

    public string ActorDisplayName { get; }

    public string CreatedAtText { get; }
}
