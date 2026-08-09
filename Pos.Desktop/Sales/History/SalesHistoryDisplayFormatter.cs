using Pos.Domain.Sales;

namespace Pos.Desktop.Sales.History;

public static class SalesHistoryDisplayFormatter
{
    public static string ToMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Efectivo",
        PaymentMethod.Card => "Tarjeta",
        PaymentMethod.BankTransfer => "Transferencia bancaria",
        _ => method.ToString(),
    };

    public static string ToStatusLabel(SaleStatus status) => status switch
    {
        SaleStatus.Completed => "Completada",
        SaleStatus.Draft => "Borrador",
        _ => status.ToString(),
    };
}
