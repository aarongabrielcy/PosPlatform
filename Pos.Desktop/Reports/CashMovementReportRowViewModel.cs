using System.Globalization;
using Pos.Application.Reports;
using Pos.Domain.CashMovements;

namespace Pos.Desktop.Reports;

// Fila del reporte "Movimientos de caja" (sección 13 de la tarea): solo lectura, sin comandos de
// edición/eliminación.
public sealed class CashMovementReportRowViewModel
{
    public CashMovementReportEntry Entry { get; }

    public string RegisterName => Entry.RegisterName;

    public string TypeText { get; }

    public string AmountText { get; }

    public string Reason => Entry.Reason;

    public string ActorDisplayName => Entry.ActorDisplayName;

    public string CreatedAtLocalText { get; }

    public CashMovementReportRowViewModel(CashMovementReportEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        TypeText = entry.Type == CashMovementType.CashIn ? "Entrada" : "Salida";
        AmountText = $"{entry.Amount.ToString("N2", CultureInfo.CurrentCulture)} {entry.Currency}";
        CreatedAtLocalText = entry.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}
