using System.Globalization;
using System.Linq;
using Pos.Application.Sales.History;

namespace Pos.Desktop.Sales.History;

// Fila del listado Ventas > Historial (TAREA 25B, sección 20): envuelve SalesHistoryItem con texto
// ya formateado para XAML, mismo criterio que ProductAuditRowViewModel/InventoryMovementRowViewModel.
public sealed class SalesHistoryRowViewModel
{
    public SalesHistoryItem Item { get; }

    public string CompletedAtLocalText { get; }

    // No existe un número humano de ticket persistido (sección 26): se muestra un fragmento corto
    // del SaleId real como identificador visual; el detalle expone el SaleId completo.
    public string SaleIdShortText { get; }

    public string CashierDisplayName => Item.CashierDisplayName;

    public string RegisterName => Item.RegisterName;

    public string ItemCountText { get; }

    public string TotalText { get; }

    public string PaymentSummaryText { get; }

    public SalesHistoryRowViewModel(SalesHistoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
        CompletedAtLocalText = item.CompletedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
        SaleIdShortText = item.SaleId.Value.ToString("N", CultureInfo.InvariantCulture)[..8].ToUpperInvariant();
        ItemCountText = item.ItemCount.ToString("0.##", CultureInfo.CurrentCulture);
        TotalText = $"{item.Total.ToString("N2", CultureInfo.CurrentCulture)} {item.Currency}";
        PaymentSummaryText = item.PaymentSummary.Count == 0
            ? "-"
            : string.Join(", ", item.PaymentSummary.Select(payment =>
                $"{SalesHistoryDisplayFormatter.ToMethodLabel(payment.Method)} {payment.Amount.ToString("N2", CultureInfo.CurrentCulture)}"));
    }
}
